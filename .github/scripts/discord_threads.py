"""Route GitHub metadata into durable per-topic Discord threads. Standard library only."""
import hashlib
import json
import os
import re
import sys
import time
from dataclasses import dataclass
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode
from urllib.request import Request, urlopen


class ApiError(RuntimeError):
    def __init__(self, service, status, code=None):
        self.status, self.code = status, code
        super().__init__(f"{service} API failed (HTTP {status}, code {code}).")


class Api:
    def __init__(self, service, base, token, scheme, sleep=time.sleep):
        self.service, self.base, self.token = service, base, token
        self.scheme, self.sleep = scheme, sleep

    def call(self, method, path, data=None):
        for attempt in range(6):
            headers = {"Authorization": f"{self.scheme} {self.token}",
                       "User-Agent": "TeamHJD-Discord-Threads/1.0", "Content-Type": "application/json"}
            if self.service == "GitHub":
                headers.update({"Accept": "application/vnd.github+json", "X-GitHub-Api-Version": "2022-11-28"})
            request = Request(self.base + path, data=None if data is None else json.dumps(data).encode(),
                              headers=headers, method=method)
            try:
                with urlopen(request, timeout=30) as response:
                    raw = response.read()
                    return json.loads(raw) if raw else None
            except HTTPError as error:
                try:
                    detail = json.loads(error.read())
                except (ValueError, UnicodeError):
                    detail = {}
                delay = detail.get("retry_after", error.headers.get("Retry-After", 2 ** attempt))
                if attempt < 5 and (error.code == 429 or error.code >= 500):
                    self.sleep(min(60, max(.1, float(delay))))
                    continue
                raise ApiError(self.service, error.code, detail.get("code")) from None
            except (URLError, TimeoutError):
                if attempt == 5:
                    raise ApiError(self.service, "network") from None
                self.sleep(2 ** attempt)
        raise AssertionError("Unreachable")

    def pages(self, path):
        for page in range(1, 1001):
            separator = "&" if "?" in path else "?"
            rows = self.call("GET", f"{path}{separator}per_page=100&page={page}")
            yield from rows
            if len(rows) < 100:
                return
        raise RuntimeError("GitHub pagination limit reached; refusing a partial route.")


@dataclass(frozen=True)
class Topic:
    repo: str
    kind: str
    number: int
    title: str
    url: str

    @property
    def key(self):
        return f"github-thread:{self.repo}:{self.kind}:{self.number}"

    @property
    def prefix(self):
        return f"[{self.kind.upper()} #{self.number}] "

    @property
    def name(self):
        return clip(self.prefix + self.repo.split("/")[1] + " · " + self.title, 100)


def clip(value, limit):
    value = str(value or "")
    result, used = [], 0
    for char in value:
        units = 2 if ord(char) > 0xFFFF else 1
        if used + units > limit:
            break
        result.append(char)
        used += units
    return "".join(result)


def nonce(key):
    return hashlib.sha256(key.encode()).hexdigest()[:24]


def topic_from(repo, kind, obj):
    number = int(obj["number"])
    if number <= 0 or not re.fullmatch(r"[\w.-]+/[\w.-]+", repo):
        raise ValueError("Invalid repository/topic identity.")
    path = {"issue": "issues", "pr": "pull", "discussion": "discussions"}[kind]
    return Topic(repo, kind, number, obj.get("title", ""),
                 f"https://github.com/{repo}/{path}/{number}")


def route(event, payload, github):
    repo = payload["repository"]["full_name"]
    if event == "workflow_run":
        run = payload["workflow_run"]
        if run.get("event") != "pull_request" or run.get("conclusion") != "failure":
            return []
        prs = run.get("pull_requests", [])
        if not prs:
            sha = run.get("head_sha", "")
            if not re.fullmatch(r"[a-fA-F0-9]{40,64}", sha):
                raise ValueError("PR CI run has no valid commit identity.")
            prs = [pr for pr in github.pages(f"/repos/{repo}/commits/{sha}/pulls")
                   if pr.get("base", {}).get("repo", {}).get("full_name", "").lower() == repo.lower()]
        if not prs:
            raise RuntimeError("Failed PR CI run could not be linked to a PR; no unrelated channel fallback was used.")
        numbers = sorted({int(pr["number"]) for pr in prs})
        return [topic_from(repo, "pr", github.call("GET", f"/repos/{repo}/pulls/{number}")) for number in numbers]
    if event == "issues":
        return [topic_from(repo, "issue", payload["issue"])]
    if event == "issue_comment":
        obj = payload["issue"]
        return [topic_from(repo, "pr" if "pull_request" in obj else "issue", obj)]
    if event in ("pull_request_target", "pull_request", "pull_request_review", "pull_request_review_comment"):
        return [topic_from(repo, "pr", payload["pull_request"])]
    if event in ("discussion", "discussion_comment"):
        return [topic_from(repo, "discussion", payload["discussion"])]
    return []


def current_closed(topic, github):
    if topic.kind == "discussion":
        owner, name = topic.repo.split("/")
        result = github.call("POST", "/graphql", {
            "query": "query($owner:String!,$name:String!,$number:Int!){repository(owner:$owner,name:$name){discussion(number:$number){closed}}}",
            "variables": {"owner": owner, "name": name, "number": topic.number}})
        if result.get("errors"):
            raise RuntimeError("Could not resolve the current Discussion state.")
        discussion = (result.get("data", {}).get("repository") or {}).get("discussion")
        return True if discussion is None else discussion["closed"]
    path = "pulls" if topic.kind == "pr" else "issues"
    try:
        return github.call("GET", f"/repos/{topic.repo}/{path}/{topic.number}")["state"] == "closed"
    except ApiError as error:
        if error.status == 404:
            return True
        raise


def marker(message):
    return [embed.get("footer", {}).get("text") for embed in message.get("embeds", [])]


class Threads:
    def __init__(self, discord, guild, channels):
        self.api, self.guild, self.channels = discord, guild, channels
        self.bot = None

    def check(self):
        self.bot = self.api.call("GET", "/users/@me")
        if not self.bot.get("bot"):
            raise ValueError("Use a Discord bot token, not a user token.")
        for kind, channel_id in self.channels.items():
            channel = self.api.call("GET", f"/channels/{channel_id}")
            if channel.get("guild_id") != self.guild or channel.get("type") not in (0, 5):
                raise ValueError(f"{kind} must be a text/announcement channel in the configured server.")
            # These reads check channel visibility/history without posting test messages.
            self.api.call("GET", f"/channels/{channel_id}/messages?limit=1")
            self.api.call("GET", f"/channels/{channel_id}/threads/archived/public?limit=1")
        self.api.call("GET", f"/guilds/{self.guild}/threads/active")

    def messages(self, channel):
        before = None
        while True:
            query = {"limit": 100}
            if before:
                query["before"] = before
            rows = self.api.call("GET", f"/channels/{channel}/messages?{urlencode(query)}")
            yield from rows
            if len(rows) < 100:
                return
            cursor = rows[-1]["id"]
            if cursor == before:
                raise RuntimeError("Discord message history did not advance.")
            before = cursor

    def matches(self, channel, thread, topic):
        if thread.get("parent_id") != channel or not thread.get("name", "").startswith(topic.prefix):
            return False
        root = self.api.call("GET", f"/channels/{channel}/messages/{thread['id']}")
        return root.get("author", {}).get("id") == self.bot["id"] and topic.key in marker(root)

    def find(self, channel, topic):
        active = self.api.call("GET", f"/guilds/{self.guild}/threads/active")
        for thread in active["threads"]:
            if self.matches(channel, thread, topic):
                return thread
        before = None
        while True:
            query = {"limit": 100}
            if before:
                query["before"] = before
            page = self.api.call("GET", f"/channels/{channel}/threads/archived/public?{urlencode(query)}")
            for thread in page["threads"]:
                if self.matches(channel, thread, topic):
                    return thread
            if not page.get("has_more"):
                break
            cursor = page["threads"][-1]["thread_metadata"]["archive_timestamp"] if page["threads"] else None
            if not cursor or cursor == before:
                raise RuntimeError("Discord archived history did not advance.")
            before = cursor
        # Recover an interrupted first delivery or a manually renamed thread using its immutable root marker.
        for root in self.messages(channel):
            if root.get("author", {}).get("id") == self.bot["id"] and topic.key in marker(root):
                return self.start(channel, root["id"], topic)
        return None

    def start(self, channel, root_id, topic):
        try:
            return self.api.call("POST", f"/channels/{channel}/messages/{root_id}/threads",
                                 {"name": topic.name, "auto_archive_duration": 1440})
        except ApiError as error:
            if error.status != 400 or error.code != 160004:
                raise
            # Discord permits exactly one thread for a source message, including concurrent first deliveries.
            thread = self.api.call("GET", f"/channels/{root_id}")
            if thread.get("parent_id") != channel:
                raise ValueError("The existing thread belongs to another channel.")
            return thread

    def ensure(self, topic):
        channel = self.channels[topic.kind]
        existing = self.find(channel, topic)
        if existing:
            return existing
        root = self.api.call("POST", f"/channels/{channel}/messages", {
            "allowed_mentions": {"parse": []}, "nonce": nonce(topic.key), "enforce_nonce": True,
            "embeds": [{"title": clip(f"{topic.kind.upper()} #{topic.number} · {topic.title}", 230),
                        "url": topic.url, "description": "이 항목의 변경·댓글·리뷰 알림은 이 스레드에 모입니다.",
                        "footer": {"text": topic.key}}]})
        return self.start(channel, root["id"], topic)

    def deliver(self, topic, embed, event_key, closed):
        thread = self.ensure(topic)
        thread_id = thread["id"]
        if thread.get("thread_metadata", {}).get("locked"):
            raise RuntimeError("The topic thread is moderator-locked; refusing to create a duplicate or unlock it.")
        if thread.get("thread_metadata", {}).get("archived"):
            self.api.call("PATCH", f"/channels/{thread_id}", {"archived": False})
        receipt = f"github-event:{nonce(topic.key + ':' + event_key)}"
        if not any(receipt in marker(message) for message in self.messages(thread_id)):
            embed = dict(embed, footer={"text": receipt})
            self.api.call("POST", f"/channels/{thread_id}/messages", {
                "embeds": [embed], "allowed_mentions": {"parse": []},
                "nonce": nonce(receipt), "enforce_nonce": True})
        # Read current GitHub state after sending, not an older queued close/reopen event.
        if closed():
            self.api.call("PATCH", f"/channels/{thread_id}", {"archived": True})
        print(f"Delivered {topic.kind} #{topic.number} to its Discord thread.")


def make_embed(topic, event, payload):
    obj = payload.get("comment") or payload.get("review") or payload.get("discussion") or payload.get("pull_request") or payload.get("issue") or {}
    action = payload.get("action", "updated")
    if event == "workflow_run":
        run = payload["workflow_run"]
        return {"title": clip(f"PR #{topic.number} · CI 실패", 230), "url": run["html_url"],
                "description": clip(f"{run.get('name', 'PR CI')} 실패 · {run.get('head_sha', '')[:12]}\n실행 결과에서 실패한 검사를 확인하세요.", 3500),
                "color": 0xD1242F}
    if payload.get("pull_request", {}).get("merged") and action == "closed":
        action = "merged"
    actor = payload.get("sender", {}).get("login", "GitHub")
    body = obj.get("body") or ""
    if event == "pull_request_review":
        body = f"리뷰 결과: {obj.get('state', 'unknown')}\n\n" + body
    if obj.get("path"):
        body = f"`{clip(obj['path'], 220)}:{obj.get('line') or obj.get('original_line') or ''}`\n\n" + body
    extras = []
    for key in ("label", "assignee", "requested_reviewer", "milestone"):
        if payload.get(key):
            item = payload[key]
            extras.append(f"{key}: {item.get('name') or item.get('login') or item.get('title', '')}")
    if extras:
        body = "\n".join(extras) + "\n\n" + body
    return {"title": clip(f"{topic.kind.upper()} #{topic.number} · {event}: {action}", 230),
            "url": obj.get("html_url") or topic.url,
            "description": clip(body, 3500) or f"{actor} · {action}",
            "color": {"issue": 0x2DA44E, "pr": 0x8957E5, "discussion": 0x0969DA}[topic.kind],
            "author": {"name": clip(actor, 100)}}


def main():
    config = json.loads((Path(__file__).parents[1] / "discord-thread-channels.json").read_text())
    token = os.environ.get("DISCORD_BOT_TOKEN", "")
    if not token:
        raise ValueError("Register DISCORD_BOT_TOKEN as an Actions repository secret first.")
    discord = Api("Discord", "https://discord.com/api/v10", token, "Bot")
    threads = Threads(discord, config["guild_id"], config["channels"])
    threads.check()
    if os.environ.get("DISCORD_CHECK_ONLY") == "true":
        print("Bot identity and configured channel history are accessible; no messages sent. Test write permissions during cutover.")
        return
    event = os.environ["GITHUB_EVENT_NAME"]
    if event == "workflow_dispatch":
        repo = os.environ["GITHUB_REPOSITORY"]
        for kind in ("issue", "pr", "discussion"):
            topic = Topic(repo, kind, 0, "[TEST] 알림 스레드 연결 확인", f"https://github.com/{repo}")
            threads.deliver(topic, {"title": "[TEST] Discord 알림 연결 확인", "description": "실제 GitHub 이슈·PR·Discussion을 만들지 않는 연결 테스트입니다."},
                            os.environ["GITHUB_RUN_ID"], lambda: True)
        return
    payload = json.loads(Path(os.environ["GITHUB_EVENT_PATH"]).read_text())
    if payload.get("repository", {}).get("full_name") != os.environ["GITHUB_REPOSITORY"]:
        raise ValueError("Event repository mismatch.")
    github = Api("GitHub", "https://api.github.com", os.environ["GITHUB_TOKEN"], "Bearer")
    topics = route(event, payload, github)
    if not topics:
        print("No associated PR/topic found; no unrelated channel fallback was used.")
        return
    # run_id is stable across a GitHub Actions rerun; each topic has its own persistent receipt.
    event_key = os.environ["GITHUB_RUN_ID"]
    for topic in topics:
        threads.deliver(topic, make_embed(topic, event, payload), event_key,
                        lambda topic=topic: current_closed(topic, github))


if __name__ == "__main__":
    try:
        main()
    except (ApiError, ValueError, RuntimeError, KeyError) as error:
        print(f"Notification failed: {error}", file=sys.stderr)
        sys.exit(1)
