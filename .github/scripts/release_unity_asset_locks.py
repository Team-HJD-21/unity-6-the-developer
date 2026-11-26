import hashlib
import json
import re
import subprocess


TAG_PREFIX = "refs/tags/unity-asset-locks/"
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


def plan_release(locks, main, tags):
    for lock in locks:
        path = lock["path"]
        if not is_lockable(path) or not lock["id"]:
            continue

        lock_id = str(lock["id"])
        tag_prefix = submission_tag(lock_id, "")
        submissions = [ref for ref in tags if ref.startswith(tag_prefix)]
        if len(submissions) != 1:
            continue

        tag = submissions[0]
        anchor = tags[tag]
        version = blob(main, path)
        if tag != submission_tag(lock_id, version) or anchor == main:
            continue
        if git("merge-base", "--is-ancestor", anchor, main, missing_ok=True) is None:
            continue
        if version != blob(anchor, path):
            yield lock_id, path, tag, anchor


def release_merged_locks(api):
    main_ref = "refs/heads/main"
    refs = {
        ref: oid for oid, ref in (
            line.split("\t", 1) for line in git("ls-remote", "--refs", "origin", main_ref, f"{TAG_PREFIX}*").splitlines()
        )
    }
    main = git("rev-parse", "HEAD")
    if refs.pop(main_ref, None) != main or not re.fullmatch(r"[0-9a-f]{40}", main):
        raise RuntimeError("main advanced during checkout. Rerun against the latest main.")

    locks = api.list_locks()
    candidates = list(plan_release(locks, main, refs))
    print(f"main {main[:12]}: {len(locks)} remote locks, {len(refs)} submission tags, {len(candidates)} eligible releases.")
    failures = []
    for lock_id, path, tag, anchor in candidates:
        try:
            try:
                api.unlock(lock_id)
            except RuntimeError:
                # Another run or an administrator may have just released this ID.
                if any(str(current["id"]) == lock_id for current in api.list_locks()):
                    raise
            print(f"Unlocked {path!r} (ID {lock_id}).")
            git("push", "--no-verify", f"--force-with-lease={tag}:{anchor}", "origin", f":{tag}")
        except RuntimeError as error:
            print(f"::error::{path}: {error}")
            failures.append(path)
    if failures:
        raise RuntimeError(f"{len(failures)} Unity lock cleanup operations failed after checking all candidates.")


if __name__ == "__main__":
    release_merged_locks(GitLfsLocks())
