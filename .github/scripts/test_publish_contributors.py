import json
import os
import unittest
from datetime import datetime, timedelta, timezone
from unittest.mock import patch
from urllib.error import URLError
from urllib.parse import parse_qs, urlsplit

import publish_contributors as dashboard
from PIL import Image, ImageColor, ImageDraw


NOW = datetime(2026, 10, 26, 12, tzinfo=timezone.utc)
CUTOFF = NOW - timedelta(days=30)


def person(login, user_id=None):
    result = {"login": login, "avatar_url": "https://example.invalid/avatar.png"}
    if user_id is not None:
        result["id"] = user_id
    return result


def review(login, submitted=NOW, state="APPROVED"):
    return {"user": person(login) if login else None,
            "submitted_at": submitted.isoformat() if submitted else None, "state": state}


def pull(number, updated=NOW, owner="Owner"):
    return {"number": number, "updated_at": updated.isoformat(), "user": person(owner)}


class ReviewActivityTests(unittest.TestCase):
    def collect(self, pulls, reviews):
        def fetch(url, token=None):
            if "/reviews?" in url:
                number = int(url.split("/pulls/")[1].split("/")[0])
                return json.dumps(reviews[number]).encode()
            return json.dumps(pulls).encode()
        with patch.object(dashboard, "fetch", side_effect=fetch):
            return dashboard.reviewed_prs("owner/repo", "read-token", NOW)

    def test_counts_distinct_prs_per_reviewer_across_review_states(self):
        activity = self.collect([pull(1), pull(2)], {
            1: [review("Alice"), review("alice", state="CHANGES_REQUESTED"),
                review("Bob", state="COMMENTED"), review("Carol", state="DISMISSED")],
            2: [review("Alice")]})
        self.assertEqual(activity["alice"]["prs"], {1, 2})
        self.assertEqual(activity["bob"]["prs"], {1})
        self.assertEqual(activity["carol"]["prs"], {1})

    def test_all_time_totals_and_recent_chart_have_separate_time_windows(self):
        activity = self.collect([pull(100)], {100: [
            review("AtCutoff", CUTOFF), review("AtNow", NOW),
            review("Old", CUTOFF - timedelta(microseconds=1)),
            review("Future", NOW + timedelta(microseconds=1))]})
        self.assertEqual(set(activity), {"atcutoff", "atnow", "old"})
        self.assertEqual(activity["atcutoff"]["recent"], {100: CUTOFF})
        self.assertEqual(activity["old"]["recent"], {})

    def test_old_pr_and_old_review_still_count_towards_total(self):
        activity = self.collect([pull(1, CUTOFF - timedelta(days=365))],
                                {1: [review("Old", CUTOFF - timedelta(days=365))]})
        self.assertEqual(activity["old"]["prs"], {1})
        self.assertEqual(activity["old"]["recent"], {})

    def test_excludes_own_pr_reviews_including_case_changes(self):
        activity = self.collect([pull(1, owner="ALICE"), pull(2, owner="Bob")], {
            1: [review("alice", state="COMMENTED"), review("Carol")],
            2: [review("Bob", state="CHANGES_REQUESTED"), review("alice")]})
        self.assertEqual(set(activity), {"carol", "alice"})
        self.assertEqual(activity["alice"]["prs"], {2})

    def test_author_identity_survives_a_login_change(self):
        own_pr = pull(1, owner="OldLogin")
        own_pr["user"]["id"] = 42
        own_review = review("NewLogin")
        own_review["user"]["id"] = 42
        self.assertEqual(self.collect([own_pr], {1: [own_review]}), {})

    def test_recent_repeat_reviews_use_first_submission_within_window(self):
        first_recent = NOW - timedelta(days=20)
        activity = self.collect([pull(1)], {1: [
            review("Alice", NOW - timedelta(days=90)),
            review("Alice", NOW - timedelta(days=1)), review("Alice", first_recent)]})
        self.assertEqual(activity["alice"]["prs"], {1})
        self.assertEqual(activity["alice"]["recent"], {1: first_recent})

    def test_pending_unsubmitted_and_deleted_users_do_not_count(self):
        activity = self.collect([pull(1)], {1: [
            review("Draft", state="PENDING"), review("Unsubmitted", submitted=None), review(None)]})
        self.assertEqual(activity, {})

    def test_empty_repository_stops_after_first_page(self):
        with patch.object(dashboard, "fetch", return_value=b"[]") as fetch:
            self.assertEqual(dashboard.reviewed_prs("owner/repo", None, NOW), {})
        self.assertEqual(fetch.call_count, 1)

    def test_paginates_both_prs_and_reviews(self):
        def fetch(url, token=None):
            page = parse_qs(urlsplit(url).query)["page"][0]
            if "/reviews?" in url:
                if "/pulls/1/" in url:
                    data = [review("Alice")] * 100 if page == "1" else [review("Bob")]
                elif "/pulls/101/" in url:
                    data = [review("ReviewOnly")]
                else:
                    data = []
            else:
                data = [pull(number) for number in range(1, 101)] if page == "1" else [pull(101)]
            return json.dumps(data).encode()
        with patch.object(dashboard, "fetch", side_effect=fetch) as api:
            activity = dashboard.reviewed_prs("owner/repo", None, NOW)
        self.assertEqual({key: item["prs"] for key, item in activity.items()},
                         {"alice": {1}, "bob": {1}, "reviewonly": {101}})
        self.assertTrue(any("/pulls/1/reviews?per_page=100&page=2" in call.args[0] for call in api.call_args_list))

    def test_api_failure_propagates_instead_of_faking_zero_reviews(self):
        with patch.object(dashboard, "fetch", side_effect=URLError("unavailable")):
            with self.assertRaises(URLError):
                dashboard.reviewed_prs("owner/repo", None, NOW)

    def test_review_failure_prevents_discord_delivery(self):
        def fetch(url, token=None):
            if "/stats/contributors" in url:
                return b"[]"
            if "/reviews?" in url:
                raise URLError("unavailable")
            return json.dumps([pull(1, datetime.now(timezone.utc))]).encode()
        with patch.dict(os.environ, {"DISCORD_CONTRIBUTORS_WEBHOOK": "https://example.invalid/webhook"}), \
             patch("sys.argv", ["publish_contributors.py", "--send"]), \
             patch.object(dashboard, "fetch", side_effect=fetch), \
             patch.object(dashboard, "urlopen") as delivery:
            with self.assertRaises(URLError):
                dashboard.main()
            delivery.assert_not_called()


class ContributorCardTests(unittest.TestCase):
    def test_adds_review_only_contributors_and_preserves_commit_totals(self):
        current_week = dashboard.week_start(NOW)
        old_week = dashboard.week_start(NOW - timedelta(days=90))
        statistics = [{"author": person("Alice"), "weeks": [
            {"w": old_week, "c": 4, "a": 30, "d": 5}, {"w": current_week, "c": 2, "a": 10, "d": 1}]}]
        reviews = {"alice": {"author": person("alice"), "prs": {1, 2}, "recent": {2: NOW}},
                   "reviewonly": {"author": person("ReviewOnly"), "prs": {1}, "recent": {1: NOW}}}
        cards = {card["author"]["login"].lower(): card
                 for card in dashboard.contributor_cards(statistics, reviews, [current_week])}
        self.assertEqual((cards["alice"]["commits"], cards["alice"]["reviews"], cards["alice"]["weekly"]), (6, 2, [2]))
        self.assertEqual(cards["alice"]["weekly_reviews"], [1])
        self.assertEqual((cards["alice"]["additions"], cards["alice"]["deletions"]), (40, 6))
        self.assertEqual((cards["reviewonly"]["commits"], cards["reviewonly"]["reviews"], cards["reviewonly"]["weekly"]), (0, 1, [0]))

    def test_zero_reviews_and_deleted_commit_authors(self):
        cards = dashboard.contributor_cards([
            {"author": person("Alice"), "weeks": [{"w": 1, "c": 1, "a": 1, "d": 0}]},
            {"author": None, "weeks": []}], {}, [1])
        self.assertEqual(len(cards), 1)
        self.assertEqual(cards[0]["reviews"], 0)

    def test_recent_rereview_of_an_old_pr_appears_once_in_the_recent_bars(self):
        activity = {"alice": {"author": person("Alice"), "prs": {1, 2}, "recent": {1: NOW}}}
        card = dashboard.contributor_cards([], activity, dashboard.recent_weeks(NOW))[0]
        self.assertEqual(card["reviews"], 2)
        self.assertEqual(sum(card["weekly_reviews"]), 1)


class ChartTests(unittest.TestCase):
    def test_commits_and_reviews_are_adjacent_blue_and_purple_bars(self):
        canvas = Image.new("RGB", (150, 90), "black")
        dashboard.chart(ImageDraw.Draw(canvas), (0, 0, 100, 40), [dashboard.week_start(NOW)], [2], [1])
        self.assertEqual(canvas.getpixel((20, 25)), ImageColor.getrgb(dashboard.COMMIT_COLOR))
        self.assertEqual(canvas.getpixel((65, 35)), ImageColor.getrgb(dashboard.REVIEW_COLOR))

    def test_week_buckets_use_the_same_utc_sunday_as_github(self):
        monday = datetime(2026, 10, 26, 1, tzinfo=timezone(timedelta(hours=9)))
        self.assertEqual(dashboard.week_start(monday), int(datetime(2026, 10, 25, tzinfo=timezone.utc).timestamp()))


if __name__ == "__main__":
    unittest.main()
