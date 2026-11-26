import unittest
from unittest.mock import Mock, call, patch

import release_unity_asset_locks as release


class ReleaseUnityAssetLocksTests(unittest.TestCase):
    anchor = "a" * 40
    main = "b" * 40
    old = "c" * 40
    submitted = "d" * 40
    claimed = "f" * 40
    lock_id = "lock-1"
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
        self.assertEqual(self.plan(), [release.ReleasePlan(self.lock_id, self.path, tag, self.anchor)])

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

    def test_resumes_claim_after_main_has_advanced_past_its_version(self):
        tag = release.submission_tag(self.lock_id, self.submitted)
        refs = {tag: self.claimed, release.releasing_tag(tag): self.anchor}
        versions = {self.main: "e" * 40, self.claimed: self.submitted, self.anchor: self.old}
        with patch.object(release, "git", return_value="") as git, patch.object(
            release, "blob", side_effect=lambda revision, _: versions[revision],
        ):
            result = list(release.plan_release([{"id": self.lock_id, "path": self.path}], self.main, refs))
        self.assertEqual(result, [release.ReleasePlan(self.lock_id, self.path, tag, self.anchor, self.claimed)])
        self.assertEqual(git.call_args_list, [
            call("merge-base", "--is-ancestor", self.anchor, self.claimed, missing_ok=True),
            call("merge-base", "--is-ancestor", self.claimed, self.main, missing_ok=True),
        ])

    def test_claim_without_a_retargeted_version_cannot_release(self):
        tag = release.submission_tag(self.lock_id, self.submitted)
        refs = {tag: self.anchor, release.releasing_tag(tag): self.anchor}
        with patch.object(release, "git", return_value=""), patch.object(release, "blob") as blob:
            result = list(release.plan_release([{"id": self.lock_id, "path": self.path}], self.main, refs))
        self.assertFalse(result)
        blob.assert_not_called()

    def test_claim_requires_version_at_claim_commit(self):
        tag = release.submission_tag(self.lock_id, self.submitted)
        refs = {tag: self.claimed, release.releasing_tag(tag): self.anchor}
        with patch.object(release, "git", return_value=""), patch.object(release, "blob", return_value=self.old):
            result = list(release.plan_release([{"id": self.lock_id, "path": self.path}], self.main, refs))
        self.assertFalse(result)

    def test_claim_and_cleanup_are_atomic_and_target_exact_refs(self):
        api = Mock()
        api.list_locks.return_value = [{"id": self.lock_id, "path": self.path}]
        tag = release.submission_tag(self.lock_id, self.submitted)
        marker = release.releasing_tag(tag)
        refs = f"{self.main}\trefs/heads/main\n{self.anchor}\t{tag}"
        push = ("-c", "push.followTags=false", "-c", "remote.origin.mirror=false",
                "push", "--no-verify", "--no-follow-tags", "--atomic")

        def ensure_claimed(_):
            git.assert_called_with(*push, f"--force-with-lease={tag}:{self.anchor}",
                                   f"--force-with-lease={marker}:", "origin", f"{self.main}:{tag}",
                                   f"{self.anchor}:{marker}")

        api.unlock.side_effect = ensure_claimed
        with patch.object(release, "plan_release", return_value=self.plan()), patch.object(
            release, "git", side_effect=[refs, self.main, "", ""],
        ) as git:
            release.release_merged_locks(api)
        api.unlock.assert_called_once_with(self.lock_id)
        git.assert_called_with(*push, f"--force-with-lease={tag}:{self.main}",
                               f"--force-with-lease={marker}:{self.anchor}", "origin", f":{tag}", f":{marker}")

    def test_resubmission_winning_claim_race_preserves_lock(self):
        api = Mock()
        api.list_locks.return_value = [{"id": self.lock_id, "path": self.path}]
        current = release.submission_tag(self.lock_id, self.submitted)
        newer = release.submission_tag(self.lock_id, "e" * 40)
        with patch.object(release, "plan_release", return_value=self.plan()), patch.object(
            release, "remote_refs", side_effect=[
                {"refs/heads/main": self.main, current: self.anchor}, {newer: self.anchor},
            ],
        ), patch.object(release, "git", return_value=self.main), patch.object(
            release, "update_refs", side_effect=RuntimeError("stale ref"),
        ):
            release.release_merged_locks(api)
        api.unlock.assert_not_called()

    def test_claim_failure_with_unchanged_refs_is_reported(self):
        api = Mock()
        api.list_locks.return_value = [{"id": self.lock_id, "path": self.path}]
        tag = release.submission_tag(self.lock_id, self.submitted)
        with patch.object(release, "plan_release", return_value=self.plan()), patch.object(
            release, "remote_refs", side_effect=[
                {"refs/heads/main": self.main, tag: self.anchor}, {tag: self.anchor},
            ],
        ), patch.object(release, "git", return_value=self.main), patch.object(
            release, "update_refs", side_effect=RuntimeError("permission denied"),
        ):
            with self.assertRaisesRegex(RuntimeError, "1 Unity lock cleanup"):
                release.release_merged_locks(api)
        api.unlock.assert_not_called()

    def test_unlock_failure_preserves_claim_for_retry(self):
        api = Mock()
        api.list_locks.return_value = [{"id": self.lock_id, "path": self.path}]
        api.unlock.side_effect = RuntimeError("authorization failed")
        tag = release.submission_tag(self.lock_id, self.submitted)
        marker = release.releasing_tag(tag)
        plan = release.ReleasePlan(self.lock_id, self.path, tag, self.anchor, self.claimed)
        refs = {"refs/heads/main": self.main, tag: self.claimed, marker: self.anchor}
        with patch.object(release, "plan_release", return_value=[plan]), patch.object(
            release, "remote_refs", return_value=refs,
        ), patch.object(release, "git", return_value=self.main), patch.object(release, "update_refs") as update:
            with self.assertRaisesRegex(RuntimeError, "1 Unity lock cleanup"):
                release.release_merged_locks(api)
        update.assert_not_called()

    def test_retry_clears_only_orphaned_claims(self):
        api = Mock()
        api.list_locks.return_value = []
        tag = release.submission_tag(self.lock_id, self.submitted)
        marker = release.releasing_tag(tag)
        historic = release.submission_tag("historic", self.submitted)
        refs = {"refs/heads/main": self.main, tag: self.claimed, marker: self.anchor, historic: self.anchor}
        with patch.object(release, "remote_refs", return_value=refs), patch.object(
            release, "git", side_effect=[self.main, "", ""],
        ), patch.object(release, "update_refs") as update:
            release.release_merged_locks(api)
        api.unlock.assert_not_called()
        update.assert_called_once_with((tag, self.claimed, ""), (marker, self.anchor, ""))


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
