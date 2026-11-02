"""Explicit cutover/rollback after the new workflow's default-branch delivery smoke test."""
import argparse
import datetime as dt
import json
import subprocess
import sys
from pathlib import Path

REPO = "Team-HJD-21/unity-6-the-developer"


def gh(*args, body=None):
    result = subprocess.run(["gh", *args], input=None if body is None else json.dumps(body),
                            capture_output=True, text=True, encoding="utf-8")
    if result.returncode:
        raise RuntimeError("GitHub configuration request failed; check gh authentication and repository admin access.")
    return json.loads(result.stdout) if result.stdout.strip() else None


def api(path, method="GET", body=None):
    args = ["api", path, "--method", method]
    if body is not None:
        args += ["--input", "-"]
    return gh(*args, body=body)


def require_smoke():
    repo = api(f"repos/{REPO}")
    sha = api(f"repos/{REPO}/commits/{repo['default_branch']}")["sha"]
    runs = api(f"repos/{REPO}/actions/workflows/discord-threads.yml/runs?event=workflow_dispatch&status=success&per_page=30")["workflow_runs"]
    now = dt.datetime.now(dt.timezone.utc)
    for run in runs:
        date = dt.datetime.fromisoformat(run["updated_at"].replace("Z", "+00:00"))
        if run["head_sha"] == sha and run.get("display_title", "").endswith(" · delivery") and now - date < dt.timedelta(hours=1):
            return
    raise RuntimeError("Run the delivery smoke test (check_only=false) on current main successfully within one hour before cutover.")


def enable(config, backup):
    require_smoke()
    hooks = api(f"repos/{REPO}/hooks")
    by_id = {hook["id"]: hook for hook in hooks}
    saved = []
    for item in config["legacy_hooks"]:
        hook = by_id.get(item["id"])
        if hook is None:
            raise RuntimeError("A legacy hook changed; inspect the configuration before cutover.")
        saved.append({"id": hook["id"], "active": hook["active"], "events": hook["events"]})
    if backup.exists():
        raise RuntimeError("Backup already exists; refusing to overwrite rollback evidence.")
    backup.parent.mkdir(parents=True, exist_ok=True)
    backup.write_text(json.dumps({"repository": REPO, "hooks": saved}, indent=2), encoding="utf-8")
    # Activate the verified replacement before removing any old events. Failure leaves duplicates, not silence.
    gh("variable", "set", "DISCORD_THREADS_ENABLED", "--repo", REPO, "--body", "true")
    for item in config["legacy_hooks"]:
        hook = by_id[item["id"]]
        remaining = [event for event in hook["events"] if event not in item["remove_events"]]
        body = {"active": bool(remaining) and hook["active"]}
        if remaining:
            body["events"] = remaining
        api(f"repos/{REPO}/hooks/{item['id']}", "PATCH", body)
    print("Thread delivery enabled; duplicate legacy events removed. Unrelated hooks/events preserved.")


def rollback(backup):
    saved = json.loads(backup.read_text(encoding="utf-8"))
    if saved["repository"] != REPO:
        raise RuntimeError("Backup repository mismatch.")
    for hook in saved["hooks"]:
        api(f"repos/{REPO}/hooks/{int(hook['id'])}", "PATCH", {"active": hook["active"], "events": hook["events"]})
    gh("variable", "set", "DISCORD_THREADS_ENABLED", "--repo", REPO, "--body", "false")
    print("Legacy webhook events restored; threaded workflow disabled.")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=["enable", "rollback"])
    parser.add_argument("--backup", type=Path, required=True, help="Private local rollback JSON outside tracked files; contains no webhook URLs/tokens.")
    args = parser.parse_args()
    config = json.loads((Path(__file__).parents[1] / "discord-thread-channels.json").read_text())
    if args.mode == "enable":
        enable(config, args.backup)
    else:
        rollback(args.backup)


if __name__ == "__main__":
    try:
        main()
    except (RuntimeError, KeyError, ValueError, OSError) as error:
        print(f"Cutover not completed: {error}", file=sys.stderr)
        sys.exit(1)
