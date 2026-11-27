import hashlib
import json
import re
import subprocess
from typing import NamedTuple


TAG_PREFIX = "refs/tags/unity-asset-locks/"
SUBMISSION_REF = re.compile(rf"{TAG_PREFIX}[0-9a-f]{{64}}/(?:[0-9a-f]{{40}}|deleted)")
LOCKABLE_PROJECT_SETTINGS = {
    "ProjectSettings/Physics2DSettings.asset",
    "ProjectSettings/TagManager.asset",
}


def submission_tag(lock_id, version):
    key = hashlib.sha256(str(lock_id).encode("utf-8")).hexdigest()
    return f"{TAG_PREFIX}{key}/{version}"


def git(*args, missing_ok=False):
    result = subprocess.run(["git", *args], capture_output=True, text=True, encoding="utf-8", errors="replace")
    if result.returncode == 1 and missing_ok:
        return None
    if result.returncode:
        raise RuntimeError(f"git {args[0]} failed: {result.stderr.strip()}")
    return result.stdout.strip()


def blob(commit, path):
    return git("rev-parse", "--verify", "--quiet", f"{commit}:{path}", missing_ok=True) or "deleted"


def is_lockable(path):
    return path in LOCKABLE_PROJECT_SETTINGS or (
        path.startswith("Assets/") and path.endswith((".unity", ".prefab", ".png.meta"))
    )


def lfs_json(*args):
    output = git("lfs", *args, "--json", "--remote=origin")
    try:
        result = json.loads(output)
    except json.JSONDecodeError as error:
        raise RuntimeError(f"Git LFS {args[0]} returned invalid JSON.") from error
    if result is None:
        return []
    if not isinstance(result, list) or not all(isinstance(item, dict) for item in result):
        raise RuntimeError(f"Git LFS {args[0]} returned an unexpected response.")
    return result


class GitLfsLocks:
    def list_locks(self):
        # Git LFS's JSON mode can hide request errors, including errors during pagination.
        checked = git("lfs", "locks", "--remote=origin")
        locks = lfs_json("locks")
        if len(locks) != len(checked.splitlines()):
            raise RuntimeError("Git LFS lock listings differed. Rerun against the latest main.")
        return locks

    def unlock(self, lock_id):
        result = lfs_json("unlock", "--force", f"--id={lock_id}")
        if len(result) != 1 or str(result[0].get("id")) != lock_id or result[0].get("unlocked") is not True:
            raise RuntimeError(f"Git LFS did not confirm release of lock {lock_id}.")


class ReleasePlan(NamedTuple):
    lock_id: str
    path: str
    tag: str
    baseline: str
    claimed_at: str = ""


def releasing_tag(tag):
    prefix, version = tag.rsplit("/", 1)
    return f"{prefix}/releasing-{version}"


def remote_refs(*patterns):
    output = git("ls-remote", "--refs", "origin", *patterns)
    return {ref: oid for oid, ref in (line.split("\t", 1) for line in output.splitlines())}


def grouped_submissions(tags):
    groups = {}
    for ref, oid in tags.items():
        if not ref.startswith(TAG_PREFIX):
            continue
        key, separator, _ = ref[len(TAG_PREFIX):].partition("/")
        if separator:
            groups.setdefault(f"{TAG_PREFIX}{key}/", {})[ref] = oid
    return groups


def is_ancestor(ancestor, commit):
    return git("merge-base", "--is-ancestor", ancestor, commit, missing_ok=True) is not None


def read_claim(submissions, main):
    if len(submissions) != 2:
        return None
    tag = next((ref for ref in submissions if SUBMISSION_REF.fullmatch(ref)), None)
    if tag is None:
        return None
    baseline = submissions.get(releasing_tag(tag))
    claimed_at = submissions[tag]
    if not baseline or baseline == claimed_at:
        return None
    if is_ancestor(baseline, claimed_at) and is_ancestor(claimed_at, main):
        return tag, baseline, claimed_at
    return None


def plan_release(locks, main, tags):
    submissions_by_lock = grouped_submissions(tags)
    for lock in locks:
        path = lock["path"]
        if not is_lockable(path) or not lock["id"]:
            continue

        lock_id = str(lock["id"])
        submissions = submissions_by_lock.get(submission_tag(lock_id, ""), {})
        if len(submissions) == 1:
            tag, baseline = next(iter(submissions.items()))
            version = blob(main, path)
            if (tag == submission_tag(lock_id, version) and baseline != main
                    and is_ancestor(baseline, main) and version != blob(baseline, path)):
                yield ReleasePlan(lock_id, path, tag, baseline)
            continue

        claim = read_claim(submissions, main)
        if claim:
            tag, baseline, claimed_at = claim
            version = blob(claimed_at, path)
            if tag == submission_tag(lock_id, version) and version != blob(baseline, path):
                yield ReleasePlan(lock_id, path, tag, baseline, claimed_at)


def update_refs(*changes):
    leases = [f"--force-with-lease={ref}:{previous}" for ref, previous, _ in changes]
    refspecs = [f"{revision}:{ref}" for ref, _, revision in changes]
    git("-c", "push.followTags=false", "-c", "remote.origin.mirror=false",
        "push", "--no-verify", "--no-follow-tags", "--atomic", *leases, "origin", *refspecs)


def claim_release(plan, main):
    try:
        # Keep the version ref occupied so a legacy retry cannot recreate it.
        update_refs((plan.tag, plan.baseline, main),
                    (releasing_tag(plan.tag), "", plan.baseline))
        return True
    except RuntimeError:
        expected = {plan.tag: plan.baseline}
        if remote_refs(f'{submission_tag(plan.lock_id, "")}*') == expected:
            raise
        print(f"Skipped {plan.path!r} (ID {plan.lock_id}): the submission changed during release.")
        return False


def clear_claim(tag, baseline, claimed_at):
    marker = releasing_tag(tag)
    try:
        update_refs((tag, claimed_at, ""), (marker, baseline, ""))
    except RuntimeError:
        if remote_refs(tag, marker):
            raise


def release_merged_locks(api):
    main_ref = "refs/heads/main"
    refs = remote_refs(main_ref, f"{TAG_PREFIX}*")
    main = git("rev-parse", "HEAD")
    if refs.pop(main_ref, None) != main or not re.fullmatch(r"[0-9a-f]{40}", main):
        raise RuntimeError("main advanced during checkout. Rerun against the latest main.")

    locks = api.list_locks()
    candidates = list(plan_release(locks, main, refs))
    print(f"main {main[:12]}: {len(locks)} remote locks, {len(refs)} submission tags, {len(candidates)} eligible releases.")
    failures = []
    for plan in candidates:
        try:
            if not plan.claimed_at and not claim_release(plan, main):
                continue
            try:
                api.unlock(plan.lock_id)
            except RuntimeError:
                # Another run or an administrator may have just released this ID.
                if any(str(current["id"]) == plan.lock_id for current in api.list_locks()):
                    raise
            print(f"Unlocked {plan.path!r} (ID {plan.lock_id}).")
            clear_claim(plan.tag, plan.baseline, plan.claimed_at or main)
        except RuntimeError as error:
            print(f"::error::{plan.path}: {error}")
            failures.append(plan.path)

    active_prefixes = {submission_tag(lock["id"], "") for lock in locks}
    for prefix, submissions in grouped_submissions(refs).items():
        if prefix in active_prefixes:
            continue
        try:
            claim = read_claim(submissions, main)
            if claim:
                clear_claim(*claim)
        except RuntimeError as error:
            print(f"::error::{prefix}: {error}")
            failures.append(prefix)
    if failures:
        raise RuntimeError(f"{len(failures)} Unity lock cleanup operations failed after checking all candidates.")


if __name__ == "__main__":
    release_merged_locks(GitLfsLocks())
