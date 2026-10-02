"""Render a GitHub-style weekly contributor dashboard for the last 90 days."""

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


def fetch(url, token=None):
    headers = {"User-Agent": "TeamHJD-contributors", "Accept": "application/vnd.github+json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    with urlopen(Request(url, headers=headers), timeout=30) as response:
        if response.status == 202:
            raise RuntimeError("GitHub statistics are being computed")
        return response.read()


def font(size):
    for candidate in ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "C:/Windows/Fonts/arial.ttf"):
        if Path(candidate).exists():
            return ImageFont.truetype(candidate, size)
    return ImageFont.load_default(size=size)


def chart(draw, box, weeks, values):
    x, y, width, height = box
    ceiling = max(5, math.ceil(max(values, default=0) / 5) * 5)
    for step in range(5):
        row = y + height - step * height / 4
        draw.line((x, row, x + width, row), fill="#30363d")
        draw.text((x + width + 8, row - 9), str(round(ceiling * step / 4)), font=font(15), fill="#8b949e")
    pitch = width / max(1, len(weeks))
    for index, value in enumerate(values):
        if value:
            left = x + index * pitch + 2
            draw.rectangle((left, y + height - height * value / ceiling, left + max(1, pitch - 4), y + height), fill="#087cff")
    for index in range(0, len(weeks), max(1, len(weeks) // 5)):
        label = datetime.fromtimestamp(weeks[index], timezone.utc).strftime("%b %d")
        draw.text((x + index * pitch, y + height + 12), label, font=font(15), fill="#8b949e")


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
    cutoff = (now - timedelta(days=90)).timestamp()
    # Keep whole API week buckets intersecting the rolling 90-day window.
    weeks = sorted({week["w"] for item in statistics for week in item["weeks"] if week["w"] + 604800 > cutoff and week["w"] <= now.timestamp()})
    contributors = []
    for item in statistics:
        selected = {week["w"]: week for week in item["weeks"] if week["w"] in weeks}
        commits = sum(week["c"] for week in selected.values())
        if commits:
            contributors.append({"author": item["author"], "commits": commits, "additions": sum(week["a"] for week in selected.values()), "deletions": sum(week["d"] for week in selected.values()), "weekly": [selected.get(week, {}).get("c", 0) for week in weeks]})
    total = [sum(item["weekly"][index] for item in contributors) for index in range(len(weeks))]
    excluded = {"stableh", "janghosung01"}
    contributors = [item for item in contributors if item["author"]["login"].lower() not in excluded]
    contributors.sort(key=lambda item: (-item["commits"], item["author"]["login"]))
    stamp = now.strftime("%Y-%m-%d %H:%M KST")
    image = Image.new("RGB", (1400, 535 + max(1, math.ceil(len(contributors) / 2)) * 330), "#0d1117")
    draw = ImageDraw.Draw(image)
    draw.text((28, 18), "Contributors", font=font(36), fill="#f0f6fc")
    draw.text((28, 68), "Weekly commits to default branch, excluding merge commits", font=font(21), fill="#8b949e")
    draw.text((28, 105), f"{repository} | Last 90 days (whole weeks) | {stamp}", font=font(18), fill="#8b949e")
    draw.rounded_rectangle((28, 150, 1372, 510), radius=10, outline="#30363d", width=2)
    draw.text((52, 170), "Commits over time", font=font(25), fill="#f0f6fc")
    chart(draw, (60, 225, 1225, 225), weeks, total)
    if not contributors:
        draw.text((52, 550), "No contributions in this period", font=font(24), fill="#8b949e")
    for index, contributor in enumerate(contributors):
        x, y = 28 + index % 2 * 686, 535 + index // 2 * 330
        draw.rounded_rectangle((x, y, x + 658, y + 306), radius=10, outline="#30363d", width=2)
        author = contributor["author"]
        avatar = Image.open(io.BytesIO(fetch(author["avatar_url"]))).convert("RGB").resize((58, 58), Image.Resampling.LANCZOS)
        mask = Image.new("L", (58, 58), 0)
        ImageDraw.Draw(mask).ellipse((0, 0, 57, 57), fill=255)
        image.paste(avatar, (x + 24, y + 24), mask)
        draw.text((x + 96, y + 23), author["login"], font=font(25), fill="#4493f8")
        draw.text((x + 96, y + 59), f'{contributor["commits"]:,} commits', font=font(17), fill="#8b949e")
        draw.text((x + 96, y + 85), f'+{contributor["additions"]:,} lines', font=font(17), fill="#3fb950")
        draw.text((x + 330, y + 85), f'-{contributor["deletions"]:,} lines', font=font(17), fill="#f0883e")
        draw.text((x + 588, y + 25), f"#{index + 1}", font=font(18), fill="#f0f6fc")
        chart(draw, (x + 26, y + 132, 560, 121), weeks, contributor["weekly"])
    output = Path(args.output)
    image.save(output, "PNG")
    print(f"Rendered {len(contributors)} contributors")
    if not args.send:
        return
    boundary = uuid.uuid4().hex
    payload = json.dumps({"content": "@everyone", "allowed_mentions": {"parse": ["everyone"]}}).encode()
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
