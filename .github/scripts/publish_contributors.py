import argparse
import io
import json
import math
import os
from pathlib import Path
import sys
from datetime import datetime, timezone, timedelta
from urllib.request import Request, urlopen
from urllib.error import HTTPError, URLError
import uuid
import time

from PIL import Image, ImageDraw, ImageFont

COMMIT_COLOR = "#087cff"
REVIEW_COLOR = "#a5a1ff"
TEAM_LOGINS = {"hjd5031", "yanggogi1423", "verbalkjt", "thefirefly-night", "cokingding"}


def fetch(url, token=None):
    headers = {"User-Agent": "TeamHJD-contributors", "Accept": "application/vnd.github+json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    with urlopen(Request(url, headers=headers), timeout=30) as response:
        if response.status == 202:
            raise RuntimeError("GitHub statistics are being computed")
        return response.read()


def parse_time(value):
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def same_author(left, right):
    if not left or not right:
        return False
    if left.get("id") is not None and right.get("id") is not None:
        return left["id"] == right["id"]
    return left.get("login", "").lower() == right.get("login", "").lower()


def week_start(moment):
    day = moment.astimezone(timezone.utc).replace(hour=0, minute=0, second=0, microsecond=0)
    return int((day - timedelta(days=(day.weekday() + 1) % 7)).timestamp())


def recent_weeks(now):
    return list(range(week_start(now - timedelta(days=30)), week_start(now) + 1, 604800))


def reviewed_prs(repository, token, now):
    cutoff = now - timedelta(days=30)
    activity = {}
    page = 1
    while True:
        pulls = json.loads(fetch(
            f"https://api.github.com/repos/{repository}/pulls?state=all&sort=updated&direction=desc&per_page=100&page={page}", token))
        if not pulls:
            return activity
        for pull in pulls:
            review_page = 1
            while True:
                reviews = json.loads(fetch(
                    f"https://api.github.com/repos/{repository}/pulls/{pull['number']}/reviews?per_page=100&page={review_page}", token))
                for review in reviews:
                    author = review.get("user")
                    submitted = review.get("submitted_at")
                    if not author or not submitted or review.get("state") == "PENDING" or same_author(author, pull.get("user")):
                        continue
                    submitted_at = parse_time(submitted)
                    if submitted_at > now:
                        continue
                    key = author["login"].lower()
                    entry = activity.setdefault(key, {"author": author, "prs": set(), "recent": {}})
                    entry["prs"].add(pull["number"])  # 같은 사람이 같은 PR에 여러 번 제출해도 1건이다.
                    if submitted_at >= cutoff:
                        previous = entry["recent"].get(pull["number"], submitted_at)
                        entry["recent"][pull["number"]] = min(previous, submitted_at)  # 최근 기간의 첫 리뷰 주에 한 번만 표시한다.
                if len(reviews) < 100:
                    break
                review_page += 1
        if len(pulls) < 100:
            return activity
        page += 1


def contributor_cards(statistics, reviews, weeks):
    cards = {}
    for item in statistics:
        author = item.get("author")
        if not author:
            continue
        selected = {week["w"]: week for week in item["weeks"] if week["w"] in weeks}
        commits = item.get("total", sum(week["c"] for week in item["weeks"]))
        if commits:
            cards[author["login"].lower()] = {
                "author": author, "commits": commits, "reviews": 0,
                "additions": sum(week["a"] for week in item["weeks"]),
                "deletions": sum(week["d"] for week in item["weeks"]),
                "weekly": [selected.get(week, {}).get("c", 0) for week in weeks],
                "weekly_reviews": [0] * len(weeks)}
    for key, activity in reviews.items():
        card = cards.setdefault(key, {"author": activity["author"], "commits": 0, "reviews": 0,
                                     "additions": 0, "deletions": 0, "weekly": [0] * len(weeks),
                                     "weekly_reviews": [0] * len(weeks)})
        card["reviews"] = len(activity["prs"])
        buckets = {}
        for submitted_at in activity["recent"].values():
            week = week_start(submitted_at)
            buckets[week] = buckets.get(week, 0) + 1
        card["weekly_reviews"] = [buckets.get(week, 0) for week in weeks]
    return list(cards.values())


def font(size):
    for candidate in ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "C:/Windows/Fonts/arial.ttf"):
        if Path(candidate).exists():
            return ImageFont.truetype(candidate, size)
    return ImageFont.load_default(size=size)


def chart(draw, box, weeks, commits, reviews):
    x, y, width, height = box
    ceiling = max(4, math.ceil(max([*commits, *reviews], default=0) / 4) * 4)
    for step in range(5):
        row = y + height - step * height / 4
        draw.line((x, row, x + width, row), fill="#30363d")
        draw.text((x + width + 8, row - 9), str(round(ceiling * step / 4)), font=font(15), fill="#8b949e")
    pitch = width / max(1, len(weeks))
    group_width = pitch * 0.82
    gap = min(4, pitch * 0.08)
    bar_width = (group_width - gap) / 2
    for index, pair in enumerate(zip(commits, reviews)):
        group_left = x + index * pitch + (pitch - group_width) / 2
        for offset, (value, color) in enumerate(zip(pair, (COMMIT_COLOR, REVIEW_COLOR))):
            left = group_left + offset * (bar_width + gap)
            if value:
                draw.rectangle((left, y + height - height * value / ceiling, left + bar_width, y + height), fill=color)
            draw.text((left + bar_width / 2, y + height + 5), f"{value:,}",
                      font=font(14 if width > 1000 else 12), fill=color, anchor="mt")
    for index in range(0, len(weeks), max(1, len(weeks) // 5)):
        label = datetime.fromtimestamp(weeks[index], timezone.utc).strftime("%b %d")
        draw.text((x + index * pitch, y + height + 29), label, font=font(15), fill="#8b949e")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--send", action="store_true")
    parser.add_argument("--output", default="contributors.png")
    args = parser.parse_args()
    repository = os.environ.get("GITHUB_REPOSITORY", "Team-HJD-21/unity-6-the-developer")
    token = os.environ.get("GITHUB_TOKEN")
    webhook = os.environ.get("DISCORD_CONTRIBUTORS_WEBHOOK")
    if args.send and not webhook:
        raise ValueError("DISCORD_CONTRIBUTORS_WEBHOOK is not configured")
    for attempt in range(4):
        try:
            statistics = json.loads(fetch(f"https://api.github.com/repos/{repository}/stats/contributors", token))
            break
        except RuntimeError:
            if attempt == 3:
                raise
            time.sleep(5)
    now = datetime.now(timezone(timedelta(hours=9)))
    weeks = recent_weeks(now)
    reviews = reviewed_prs(repository, token, now)
    contributors = contributor_cards(statistics, reviews, weeks)
    contributors = [item for item in contributors if item["author"]["login"].lower() in TEAM_LOGINS]
    contributors.sort(key=lambda item: (-item["commits"], item["author"]["login"]))
    total = [sum(item["weekly"][index] for item in contributors) for index in range(len(weeks))]
    total_reviews = [sum(item["weekly_reviews"][index] for item in contributors) for index in range(len(weeks))]
    stamp = now.strftime("%Y-%m-%d %H:%M KST")
    image = Image.new("RGB", (1400, 555 + max(1, math.ceil(len(contributors) / 2)) * 330), "#0d1117")
    draw = ImageDraw.Draw(image)
    draw.text((28, 18), "Contributors", font=font(36), fill="#f0f6fc")
    draw.text((28, 68), "All-time commits to default branch (excluding merges) + reviews of others' PRs", font=font(21), fill="#8b949e")
    draw.text((28, 105), f"{repository} | {stamp}", font=font(18), fill="#8b949e")
    draw.text((28, 133), "Text: all-time totals | Bars: last 30 days, grouped by week", font=font(16), fill="#8b949e")
    draw.rounded_rectangle((28, 170, 1372, 530), radius=10, outline="#30363d", width=2)
    draw.text((52, 190), "Commits and reviewed PRs", font=font(25), fill="#f0f6fc")
    for left, label, color in ((960, "Commits", COMMIT_COLOR), (1110, "Reviewed PRs", REVIEW_COLOR)):
        draw.rectangle((left, 196, left + 14, 210), fill=color)
        draw.text((left + 22, 192), label, font=font(16), fill="#8b949e")
    chart(draw, (60, 245, 1225, 225), weeks, total, total_reviews)
    if not contributors:
        draw.text((52, 570), "No contributions in this period", font=font(24), fill="#8b949e")
    for index, contributor in enumerate(contributors):
        x, y = 28 + index % 2 * 686, 555 + index // 2 * 330
        draw.rounded_rectangle((x, y, x + 658, y + 306), radius=10, outline="#30363d", width=2)
        author = contributor["author"]
        avatar = Image.open(io.BytesIO(fetch(author["avatar_url"]))).convert("RGB").resize((58, 58), Image.Resampling.LANCZOS)
        mask = Image.new("L", (58, 58), 0)
        ImageDraw.Draw(mask).ellipse((0, 0, 57, 57), fill=255)
        image.paste(avatar, (x + 24, y + 24), mask)
        draw.text((x + 96, y + 23), author["login"], font=font(25), fill="#4493f8")
        draw.text((x + 96, y + 59), f'Total {contributor["commits"]:,} commits', font=font(17), fill="#8b949e")
        draw.text((x + 330, y + 59), f'Total {contributor["reviews"]:,} reviewed PRs', font=font(17), fill=REVIEW_COLOR)
        draw.text((x + 96, y + 85), f'+{contributor["additions"]:,} lines', font=font(17), fill="#3fb950")
        draw.text((x + 330, y + 85), f'-{contributor["deletions"]:,} lines', font=font(17), fill="#f0883e")
        draw.text((x + 588, y + 25), f"#{index + 1}", font=font(18), fill="#f0f6fc")
        chart(draw, (x + 26, y + 132, 560, 121), weeks, contributor["weekly"], contributor["weekly_reviews"])
    output = Path(args.output)
    image.save(output, "PNG")
    print(f"Rendered {len(contributors)} contributors")
    if not args.send:
        return
    boundary = uuid.uuid4().hex
    is_test = os.environ.get("CONTRIBUTORS_TEST_NOTIFICATION") == "true"
    payload = json.dumps({
        "content": "[알림 테스트] 비공개 저장소 contributors 이미지 전송 확인" if is_test else "@everyone",
        "allowed_mentions": {"parse": [] if is_test else ["everyone"]},
    }).encode()
    body = (f"--{boundary}\r\nContent-Disposition: form-data; name=\"payload_json\"\r\nContent-Type: application/json\r\n\r\n".encode() + payload + f"\r\n--{boundary}\r\nContent-Disposition: form-data; name=\"files[0]\"; filename=\"contributors.png\"\r\nContent-Type: image/png\r\n\r\n".encode() + output.read_bytes() + f"\r\n--{boundary}--\r\n".encode())
    separator = "&" if "?" in webhook else "?"
    request = Request(webhook + separator + "wait=true", data=body, headers={"Content-Type": f"multipart/form-data; boundary={boundary}", "User-Agent": "TeamHJD-contributors"})
    with urlopen(request, timeout=60) as response:
        message = json.load(response)
    print(f'Discord confirmed message {message["id"]}; attachments: {len(message.get("attachments", []))}')


if __name__ == "__main__":
    try:
        main()
    except HTTPError as error:
        print(f"HTTP request failed with status {error.code}; URL omitted for credential safety", file=sys.stderr)
        sys.exit(1)
    except URLError:
        print("Network request failed; URL omitted for credential safety", file=sys.stderr)
        sys.exit(1)
    except Exception as error:
        print(f"Contributors publishing failed ({type(error).__name__}); check configuration and image inputs", file=sys.stderr)
        sys.exit(1)
