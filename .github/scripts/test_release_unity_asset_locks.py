import unittest
from unittest.mock import Mock, patch

import release_unity_asset_locks as release


class ReleaseUnityAssetLocksTests(unittest.TestCase):
    anchor = "a" * 40
    main = "b" * 40
    old = "c" * 40
    submitted = "d" * 40
    path = "Assets/Example Prefab.prefab"

    def plan(self, *, active_id="lock-1", tag_id="lock-1", path=None, anchor_version=None,
             submission=None, main_version=None, ancestor=True, ambiguous=False):
        path = path or self.path
        tag = release.submission_tag(tag_id, submission or self.submitted)
        tags = {tag: self.anchor}
        if ambiguous:
            tags[release.submission_tag(tag_id, "e" * 40)] = self.anchor
        versions = {self.main: main_version or self.submitted, self.anchor: anchor_version or self.old}
        with patch.object(release, "git", return_value="" if ancestor else None), patch.object(
            release, "blob", side_effect=lambda revision, _: versions[revision],
        ):
            return list(release.plan_release([{"id": active_id, "path": path}], self.main, tags))

    def test_recovers_matching_version_even_after_an_unrelated_main_push(self):
        tag = release.submission_tag("lock-1", self.submitted)
        self.assertEqual(self.plan(), [("lock-1", self.path, tag, self.anchor)])

    def test_does_not_release_when_submission_is_absent_or_different(self):
        self.assertFalse(self.plan(active_id="lock-2"))
        self.assertFalse(self.plan(main_version="e" * 40))

    def test_does_not_release_if_file_was_unchanged_since_lock(self):
        self.assertFalse(self.plan(anchor_version=self.submitted))

    def test_does_not_release_if_baseline_is_outside_main_history(self):
        self.assertFalse(self.plan(ancestor=False))

    def test_does_not_release_ambiguous_submission(self):
        self.assertFalse(self.plan(ambiguous=True))

    def test_submitted_deletion_unlocks(self):
        self.assertTrue(self.plan(submission="deleted", main_version="deleted"))

    def test_scope_keeps_supported_project_settings(self):
        for path in ("ProjectSettings/Physics2DSettings.asset", "ProjectSettings/TagManager.asset"):
            self.assertTrue(self.plan(path=path))
        self.assertFalse(self.plan(path="ProjectSettings/ProjectSettings.asset"))

    def test_current_main_is_required(self):
        api = Mock()
        with patch.object(release, "git", side_effect=[self.main + "\trefs/heads/main", self.anchor]):
            with self.assertRaisesRegex(RuntimeError, "main advanced"):
                release.release_merged_locks(api)
        api.list_locks.assert_not_called()

    def test_release_removes_exact_lock_id_and_its_submission_tag(self):
        api = Mock()
        api.list_locks.return_value = [{"id": "lock-1", "path": self.path}]
        tag = release.submission_tag("lock-1", self.submitted)
        refs = f"{self.main}\trefs/heads/main\n{self.anchor}\t{tag}"
        with patch.object(release, "plan_release", return_value=self.plan()), patch.object(
            release, "git", side_effect=[refs, self.main, ""],
        ) as git:
            release.release_merged_locks(api)
        api.unlock.assert_called_once_with("lock-1")
        git.assert_called_with("push", "--no-verify", f"--force-with-lease={tag}:{self.anchor}", "origin", f":{tag}")


class GitLfsLocksTests(unittest.TestCase):
    def setUp(self):
        self.api = release.GitLfsLocks()

    def test_list_reports_lfs_403_before_json_can_hide_it(self):
        with patch.object(release, "git", side_effect=RuntimeError("Git LFS HTTP 403")) as git:
            with self.assertRaisesRegex(RuntimeError, "HTTP 403"):
                self.api.list_locks()
        git.assert_called_once_with("lfs", "locks", "--remote=origin")

    def test_list_uses_origin_credentials_and_keeps_structured_paths(self):
        listing = "Assets/Example Prefab.prefab\towner\tID:16"
        response = '[{"id":"16","path":"Assets/Example Prefab.prefab"}]'
        with patch.object(release, "git", side_effect=[listing, response]) as git:
            self.assertEqual(self.api.list_locks(), [{"id": "16", "path": "Assets/Example Prefab.prefab"}])
        self.assertEqual([call.args for call in git.call_args_list], [
            ("lfs", "locks", "--remote=origin"),
            ("lfs", "locks", "--json", "--remote=origin"),
        ])

    def test_list_accepts_verified_empty_response(self):
        for response in ("[]", "null"):
            with self.subTest(response=response), patch.object(release, "git", side_effect=["", response]):
                self.assertEqual(self.api.list_locks(), [])

    def test_list_refuses_incomplete_json_results(self):
        listing = "Assets/One.prefab\towner\tID:1\nAssets/Two.prefab\towner\tID:2"
        response = '[{"id":"1","path":"Assets/One.prefab"}]'
        with patch.object(release, "git", side_effect=[listing, response]):
            with self.assertRaisesRegex(RuntimeError, "lock listings differed"):
                self.api.list_locks()

    def test_unlock_targets_exact_id(self):
        with patch.object(release, "git", return_value='[{"id":"16","unlocked":true}]') as git:
            self.api.unlock("16")
        git.assert_called_once_with("lfs", "unlock", "--force", "--id=16", "--json", "--remote=origin")

    def test_unlock_requires_matching_success_confirmation(self):
        for response in ('[]', '[{"id":"99","unlocked":true}]', '[{"id":"16","unlocked":false}]'):
            with self.subTest(response=response), patch.object(release, "git", return_value=response):
                with self.assertRaisesRegex(RuntimeError, "did not confirm"):
                    self.api.unlock("16")

    def test_malformed_unlock_response_is_reported(self):
        with patch.object(release, "git", return_value="not JSON"):
            with self.assertRaisesRegex(RuntimeError, "invalid JSON"):
                self.api.unlock("16")


if __name__ == "__main__":
    unittest.main()
