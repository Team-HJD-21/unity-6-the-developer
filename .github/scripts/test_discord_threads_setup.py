import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import configure_discord_threads as setup


class SetupTests(unittest.TestCase):
    def test_enable_requires_successful_smoke_before_any_mutation(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(setup, "require_smoke", side_effect=RuntimeError("No test")), patch.object(setup, "api") as api:
            with self.assertRaises(RuntimeError):
                setup.enable({"legacy_hooks": []}, Path(directory) / "backup.json")
            api.assert_not_called()

    def test_cutover_preserves_unrelated_events_and_excludes_tokens_from_backup(self):
        config = {"legacy_hooks": [{"id": 1, "remove_events": ["issues"]}, {"id": 2, "remove_events": ["pull_request"]}]}
        hooks = [{"id": 1, "active": True, "events": ["issues", "milestone"], "config": {"url": "TOP_SECRET"}},
                 {"id": 2, "active": True, "events": ["pull_request"], "config": {"url": "TOP_SECRET"}}]
        with tempfile.TemporaryDirectory() as directory, patch.object(setup, "require_smoke"), patch.object(setup, "gh") as gh, patch.object(setup, "api", return_value=hooks) as api:
            backup = Path(directory) / "backup.json"
            setup.enable(config, backup)
            self.assertNotIn("TOP_SECRET", backup.read_text())
            self.assertEqual({"active": True, "events": ["milestone"]}, api.call_args_list[1].args[2])
            self.assertEqual({"active": False}, api.call_args_list[2].args[2])
            self.assertIn("true", gh.call_args.args)

    def test_existing_backup_is_not_overwritten(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(setup, "require_smoke"), patch.object(setup, "api", return_value=[]), patch.object(setup, "gh") as gh:
            backup = Path(directory) / "backup.json"
            backup.write_text("existing rollback evidence")
            with self.assertRaisesRegex(RuntimeError, "already exists"):
                setup.enable({"legacy_hooks": []}, backup)
            self.assertEqual("existing rollback evidence", backup.read_text())
            gh.assert_not_called()

    def test_rollback_restores_old_hooks_before_disabling_new_workflow(self):
        calls = []
        with tempfile.TemporaryDirectory() as directory:
            backup = Path(directory) / "backup.json"
            backup.write_text(json.dumps({"repository": setup.REPO, "hooks": [{"id": 1, "active": True, "events": ["issues"]}]}))
            with patch.object(setup, "api", side_effect=lambda *args: calls.append("restore")), patch.object(setup, "gh", side_effect=lambda *args: calls.append("disable")):
                setup.rollback(backup)
        self.assertEqual(["restore", "disable"], calls)


if __name__ == "__main__":
    unittest.main()
