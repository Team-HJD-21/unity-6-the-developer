import hashlib
import json
import os
import re
import subprocess
import sys


TAG_PREFIX = "refs/tags/unity-asset-locks/"
SHA = re.compile(r"[0-9a-f]{40}")
LOCKABLE_PROJECT_SETTINGS = {
    "ProjectSettings/Physics2DSettings.asset",
    "ProjectSettings/TagManager.asset",
}


def submission_tag(lock_id, version):
    key = hashlib.sha256(str(lock_id).encode("utf-8")).hexdigest()
    return f"{TAG_PREFIX}{key}/{version}"


def git(*args, allow_failure=False):
    result = subprocess.run(["git", *args], capture_output=True, check=False)
    if result.returncode and not allow_failure:
        message = result.stderr.decode("utf-8", "replace").strip()
        raise RuntimeError(f"git {args[0]} failed: {message}")
    return result.stdout if not result.returncode else None


def blob(commit, path):
    oid = git("rev-parse", "--verify", "--quiet", f"{commit}:{path}", allow_failure=True)
    return oid.decode("ascii").strip() if oid else "deleted"


def list_locks():
    return json.loads(git("lfs", "locks", "--json", "--remote=origin"))


def release_merged_locks(before, after):
    if not SHA.fullmatch(before) or not SHA.fullmatch(after):
        raise ValueError("Expected two full commit hashes from main")
    if before == "0" * 40:
        print("No previous main revision. No locks released.")
        return

    paths = git("diff", "--name-only", "--no-renames", "-z", before, after, "--").decode("utf-8").split("\0")
    changed = {
        path for path in paths
        if path in LOCKABLE_PROJECT_SETTINGS
        or (path.startswith("Assets/") and path.endswith((".unity", ".prefab", ".png.meta")))
    }
    if not changed:
        return
    candidates = [
        lock for lock in list_locks() if lock["path"] in changed and lock["id"]
    ]
    if not candidates:
        return

    tags = {
        line.split("\t", 1)[1]
        for line in git("ls-remote", "--refs", "origin", f"{TAG_PREFIX}*")
        .decode("ascii").splitlines()
    }
    for lock in candidates:
        path = lock["path"]
        lock_id = str(lock["id"])
        version = blob(after, path)
        tag = submission_tag(lock_id, version)
        if tag not in tags or version == blob(before, path):
            continue

        try:
            git("lfs", "unlock", "--force", f"--id={lock_id}", "--remote=origin")
        except RuntimeError:
            # Two main updates can complete while the same lock is still listed.
            if any(str(current["id"]) == lock_id for current in list_locks()):
                raise
        print(f"Unlocked {path!r} after {after[:12]}")
        git("push", "--no-verify", "origin", f":{tag}")


if __name__ == "__main__":
    if os.environ.get("LOCKS_CONFIGURED") != "true":
        print("::warning::UNITY_ASSET_LOCKS_TOKEN is missing. No locks released.")
        sys.exit(0)
    release_merged_locks(os.environ["BEFORE"], os.environ["AFTER"])
