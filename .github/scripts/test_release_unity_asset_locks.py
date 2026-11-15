import json
import unittest
from unittest.mock import patch

import release_unity_asset_locks as release


class ReleaseUnityAssetLocksTests(unittest.TestCase):
    before = "a" * 40
    after = "b" * 40
    old = "c" * 40
    submitted = "d" * 40
    path = "Assets/Example Prefab.prefab"

    def run_release(self, tag_id, tag_version, new_version, *, changed=True, active_id="lock-1"):
        tag = release.submission_tag(tag_id, tag_version)
        calls = []

        def git(*args, **_):
            calls.append(args)
            if args[0] == "diff":
                return (self.path + "\0").encode() if changed else b""
            if args[:2] == ("lfs", "locks"):
                return json.dumps([{"id": active_id, "path": self.path}]).encode()
            if args[0] == "ls-remote":
                return f"{self.before}\t{tag}\n".encode()
            if args[0] == "rev-parse":
                oid = new_version if args[-1].startswith(self.after) else self.old
                return oid.encode() if oid != "deleted" else None
            if args[:2] == ("lfs", "unlock") or args[0] == "push":
                return b""
            self.fail(f"Unexpected command: {args!r}")

        with patch.object(release, "git", side_effect=git):
            release.release_merged_locks(self.before, self.after)
        return calls

    def test_merge_with_matching_version_unlocks_exact_id_and_deletes_tag(self):
        calls = self.run_release("lock-1", self.submitted, self.submitted)
        self.assertEqual(calls[-2], ("lfs", "unlock", "--force", "--id=lock-1", "--remote=origin"))
        self.assertEqual(calls[-1][-1], f":{release.submission_tag('lock-1', self.submitted)}")

    def test_mismatched_version_keeps_lock(self):
        calls = self.run_release("lock-1", self.submitted, "e" * 40)
        self.assertFalse(any(call[0] == "push" or call[:2] == ("lfs", "unlock") for call in calls))

    def test_same_path_with_new_lock_id_stays_locked(self):
        calls = self.run_release("lock-1", self.submitted, self.submitted, active_id="lock-2")
        self.assertFalse(any(call[:2] == ("lfs", "unlock") for call in calls))

    def test_unchanged_path_does_not_unlock(self):
        calls = self.run_release("lock-1", self.submitted, self.submitted, changed=False)
        self.assertFalse(any(call[:2] == ("lfs", "unlock") for call in calls))

    def test_submitted_deletion_unlocks(self):
        calls = self.run_release("lock-1", "deleted", "deleted")
        self.assertIn(("lfs", "unlock", "--force", "--id=lock-1", "--remote=origin"), calls)


if __name__ == "__main__":
    unittest.main()
