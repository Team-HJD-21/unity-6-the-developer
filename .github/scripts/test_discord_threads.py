import copy
import json
import unittest
from pathlib import Path
from unittest.mock import Mock, patch
import discord_threads as m

REPO = "Team-HJD-21/unity-6-the-developer"


def payload(number=12):
    return {"repository": {"full_name": REPO}, "action": "created", "sender": {"login": "hjd5031"},
            "issue": {"number": number, "title": "제목", "body": "본문"},
            "comment": {"id": 123, "body": "@everyone 질문", "html_url": "https://github.com/comment"}}


class FakeDiscord:
    def __init__(self):
        self.calls = []
        self.roots = {}
        self.threads = {}
        self.posts = {}
        self.nonces = {}
        self.seq = 100
        self.starts = 0

    def call(self, method, path, data=None):
        self.calls.append((method, path, copy.deepcopy(data)))
        if path == "/users/@me":
            return {"id": "bot", "bot": True}
        if path == "/guilds/g/threads/active":
            return {"threads": [copy.deepcopy(t) for t in self.threads.values() if not t["thread_metadata"]["archived"]]}
        if path.startswith("/channels/c/threads/archived/public"):
            return {"threads": [copy.deepcopy(t) for t in self.threads.values() if t["thread_metadata"]["archived"]], "has_more": False}
        if method == "GET" and path == "/channels/c":
            return {"guild_id": "g", "type": 0}
        if method == "GET" and path.startswith("/channels/c/messages/"):
            return copy.deepcopy(self.roots[path.split("/")[-1]])
        if method == "GET" and "/messages?" in path:
            channel = path.split("/")[2]
            return copy.deepcopy(list(self.roots.values()) if channel == "c" else self.posts.get(channel, []))
        if method == "POST" and path.endswith("/threads"):
            root = path.split("/")[-2]
            if root in self.threads:
                raise m.ApiError("Discord", 400, 160004)
            self.starts += 1
            self.threads[root] = {"id": root, "name": data["name"], "parent_id": "c",
                                  "thread_metadata": {"archived": False, "locked": False}}
            return copy.deepcopy(self.threads[root])
        if method == "GET" and path.startswith("/channels/"):
            return copy.deepcopy(self.threads[path.split("/")[-1]])
        if method == "POST" and path.endswith("/messages"):
            channel = path.split("/")[2]
            key = data["nonce"]
            if key in self.nonces:
                return copy.deepcopy(self.nonces[key])
            self.seq += 1
            obj = dict(copy.deepcopy(data), id=str(self.seq), author={"id": "bot"})
            self.nonces[key] = obj
            if channel == "c":
                self.roots[obj["id"]] = obj
            else:
                self.posts.setdefault(channel, []).append(obj)
            return copy.deepcopy(obj)
        if method == "PATCH":
            obj = self.threads[path.split("/")[-1]]
            obj["thread_metadata"].update(data)
            return copy.deepcopy(obj)
        raise AssertionError((method, path, data))


class RoutingTests(unittest.TestCase):
    def test_issue_comment_and_pr_conversation_comment_are_separate(self):
        event = payload()
        self.assertEqual("issue", m.route("issue_comment", event, Mock())[0].kind)
        event["issue"]["pull_request"] = {"url": "api-url"}
        self.assertEqual("pr", m.route("issue_comment", event, Mock())[0].kind)

    def test_pr_lifecycle_review_inline_and_replies_share_one_key(self):
        event = payload()
        event["pull_request"] = event["issue"]
        keys = {m.route(kind, event, Mock())[0].key for kind in
                ("pull_request_target", "pull_request_review", "pull_request_review_comment")}
        self.assertEqual(1, len(keys))

    def test_discussion_comments_share_discussion_key(self):
        event = payload()
        event["discussion"] = event["issue"]
        self.assertEqual(m.route("discussion", event, Mock()), m.route("discussion_comment", event, Mock()))
        self.assertNotEqual(m.route("discussion", event, Mock())[0].key,
                            m.route("issue_comment", event, Mock())[0].key)

    def test_commit_comments_are_not_handled_by_actions(self):
        self.assertEqual([], m.route("commit_comment", payload(), Mock()))
        config = json.loads((Path(__file__).parents[1] / "discord-thread-channels.json").read_text())
        self.assertTrue(all("commit_comment" not in hook["remove_events"] for hook in config["legacy_hooks"]))

    def test_current_closed_state_comes_from_github_not_old_event(self):
        topic = m.topic_from(REPO, "pr", payload()["issue"])
        api = Mock()
        api.call.return_value = {"state": "open"}
        self.assertFalse(m.current_closed(topic, api))
        api.call.return_value = {"state": "closed"}
        self.assertTrue(m.current_closed(topic, api))

    def test_discussion_state_uses_graphql(self):
        api = Mock()
        topic = m.topic_from(REPO, "discussion", payload()["issue"])
        api.call.return_value = {"data": {"repository": {"discussion": {"closed": True}}}}
        self.assertTrue(m.current_closed(topic, api))
        self.assertEqual("/graphql", api.call.call_args.args[1])

    def test_failed_pr_ci_routes_to_its_pr_and_success_is_silent(self):
        event = payload()
        event["workflow_run"] = {"event": "pull_request", "conclusion": "failure", "pull_requests": [{"number": 33}],
                                 "html_url": "https://github.com/actions/runs/1"}
        api = Mock()
        api.call.return_value = {"number": 33, "title": "CI target"}
        topics = m.route("workflow_run", event, api)
        self.assertEqual(("pr", 33), (topics[0].kind, topics[0].number))
        self.assertIn("CI 실패", m.make_embed(topics[0], "workflow_run", event)["title"])
        event["workflow_run"]["conclusion"] = "success"
        self.assertEqual([], m.route("workflow_run", event, api))

    def test_pr_ci_with_empty_payload_list_resolves_commit_associations(self):
        event = payload()
        event["workflow_run"] = {"event": "pull_request", "conclusion": "failure", "pull_requests": [], "head_sha": "a" * 40}
        api = Mock()
        api.pages.return_value = [{"number": 33, "base": {"repo": {"full_name": REPO}}}]
        api.call.return_value = {"number": 33, "title": "PR"}
        self.assertEqual(33, m.route("workflow_run", event, api)[0].number)

    def test_main_push_ci_keeps_legacy_notification_route(self):
        event = payload()
        event["workflow_run"] = {"event": "push", "conclusion": "failure"}
        self.assertEqual([], m.route("workflow_run", event, Mock()))

    def test_unicode_limits_and_review_context(self):
        self.assertEqual("😀" * 50, m.clip("😀" * 100, 100))
        event = payload()
        del event["comment"]
        event["review"] = {"body": "리뷰", "state": "approved"}
        topic = m.topic_from(REPO, "pr", event["issue"])
        self.assertIn("approved", m.make_embed(topic, "pull_request_review", event)["description"])


class ThreadTests(unittest.TestCase):
    def setUp(self):
        self.api = FakeDiscord()
        self.threads = m.Threads(self.api, "g", {"issue": "c", "pr": "c", "discussion": "c"})
        self.threads.bot = {"id": "bot"}
        self.topic = m.topic_from(REPO, "pr", payload()["issue"])

    def test_first_event_creates_one_thread_and_next_comments_reuse_it(self):
        self.threads.deliver(self.topic, {"title": "open"}, "run1", lambda: False)
        self.threads.deliver(self.topic, {"title": "comment"}, "run2", lambda: False)
        self.assertEqual(1, self.api.starts)
        self.assertEqual(1, len(self.api.roots))
        self.assertEqual(2, len(next(iter(self.api.posts.values()))))

    def test_archived_thread_is_reused_on_late_comment_and_reclosed(self):
        self.threads.deliver(self.topic, {"title": "close"}, "run1", lambda: True)
        self.threads.deliver(self.topic, {"title": "late comment"}, "run2", lambda: True)
        self.assertEqual(1, self.api.starts)
        self.assertTrue(next(iter(self.api.threads.values()))["thread_metadata"]["archived"])
        self.assertEqual(2, len(next(iter(self.api.posts.values()))))

    def test_reopened_issue_unarchives_existing_thread(self):
        self.threads.deliver(self.topic, {"title": "close"}, "run1", lambda: True)
        self.threads.deliver(self.topic, {"title": "reopen"}, "run2", lambda: False)
        self.assertFalse(next(iter(self.api.threads.values()))["thread_metadata"]["archived"])
        self.assertEqual(1, self.api.starts)

    def test_workflow_rerun_is_deduplicated_using_durable_receipt(self):
        self.threads.deliver(self.topic, {"title": "comment"}, "stable-run", lambda: False)
        self.api.nonces.clear()  # Discord's short-lived nonce window has already expired.
        self.threads.deliver(self.topic, {"title": "comment"}, "stable-run", lambda: False)
        self.assertEqual(1, len(next(iter(self.api.posts.values()))))

    def test_creation_race_uses_same_anchor_nonce_and_thread(self):
        with patch.object(self.threads, "find", return_value=None):
            a = self.threads.ensure(self.topic)
            b = self.threads.ensure(self.topic)
        self.assertEqual(a["id"], b["id"])
        self.assertEqual(1, len(self.api.roots))
        self.assertEqual(1, self.api.starts)

    def test_orphan_anchor_and_renamed_thread_are_recovered(self):
        thread = self.threads.ensure(self.topic)
        self.api.threads[thread["id"]]["name"] = "수동으로 이름 변경"
        self.assertEqual(thread["id"], self.threads.ensure(self.topic)["id"])
        self.assertEqual(1, self.api.starts)

    def test_locked_thread_is_not_duplicated_or_unlocked(self):
        thread = self.threads.ensure(self.topic)
        self.api.threads[thread["id"]]["thread_metadata"]["locked"] = True
        with self.assertRaisesRegex(RuntimeError, "moderator-locked"):
            self.threads.deliver(self.topic, {}, "run1", lambda: False)
        self.assertEqual(1, self.api.starts)

    def test_mentions_are_disabled_on_parent_and_thread_messages(self):
        self.threads.deliver(self.topic, {"description": "@everyone <@123>"}, "run1", lambda: False)
        for method, path, data in self.api.calls:
            if method == "POST" and path.endswith("/messages"):
                self.assertEqual({"parse": []}, data["allowed_mentions"])
                self.assertTrue(data["enforce_nonce"])

    def test_channel_configuration_does_not_mix_topic_types(self):
        config = json.loads((Path(__file__).parents[1] / "discord-thread-channels.json").read_text())
        self.assertEqual(3, len(set(config["channels"].values())))

    def test_workflow_checks_out_only_trusted_default_branch_and_keeps_all_pending_events(self):
        workflow = (Path(__file__).parents[1] / "workflows/discord-threads.yml").read_text()
        self.assertIn("ref: ${{ github.event.repository.default_branch }}", workflow)
        self.assertNotIn("ref: ${{ github.event.pull_request.head", workflow)
        self.assertNotIn("\nconcurrency:", workflow)
        self.assertIn("DISCORD_THREADS_ENABLED == 'true'", workflow)
        self.assertIn("discussions: read", workflow)
        self.assertNotIn("  commit_comment:", workflow)


if __name__ == "__main__":
    unittest.main()
