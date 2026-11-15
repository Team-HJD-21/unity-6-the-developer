#!/bin/sh
# Unity asset locks (managed by tools/UnityAssetLocks/AssetLocks.ps1)
set -eu

case "$0" in
  */*) hook_dir=${0%/*} ;;
  *) hook_dir=. ;;
esac
input=$(mktemp)
trap 'rm -f "$input"' EXIT
cat > "$input"
previous="$hook_dir/pre-push.before-unity-asset-locks"
if [ -x "$previous" ]; then
  "$previous" "$@" < "$input"
fi
if ! [ -f "$previous" ] || ! grep -q 'git lfs pre-push' "$previous"; then
  git lfs pre-push "$@" < "$input"
fi
helper=tools/UnityAssetLocks/AssetLocks.ps1
if [ "$1" = origin ] && [ -f "$helper" ]; then
  if command -v powershell.exe >/dev/null 2>&1; then
    powershell.exe -NoProfile -NonInteractive -ExecutionPolicy RemoteSigned -File "$helper" Submit -InputPath "$(cygpath -w "$input")"
  else
    pwsh -NoProfile -NonInteractive -File "$helper" Submit -InputPath "$input"
  fi
fi
