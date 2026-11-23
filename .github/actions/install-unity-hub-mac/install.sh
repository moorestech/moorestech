#!/usr/bin/env bash
set -euo pipefail

# 検証済みのチェックサムは arm64 だけなので、他のランナーはここで止める
# Only the arm64 checksum is pinned, so stop here on any other runner
if [ "$(uname -m)" != "arm64" ]; then
  echo "::error::Unity Hub pin supports arm64 runners only, got $(uname -m)"
  exit 1
fi

work_dir=$(mktemp -d)
dmg_path="$work_dir/UnityHub.dmg"
mount_path="$work_dir/mount"
url="https://public-cdn.cloud.unity3d.com/hub/prod/${HUB_VERSION}/UnityHubSetup-${HUB_VERSION}-arm64.dmg"

# 取得物はチェックサムが合うものだけを使う
# Use the download only when its checksum matches
curl -fsSL --retry 3 --retry-delay 5 -o "$dmg_path" "$url"
if ! echo "$HUB_ARM64_SHA256  $dmg_path" | shasum -a 256 -c - > /dev/null; then
  echo "::error::Unity Hub ${HUB_VERSION} checksum mismatch"
  exit 1
fi

# 使用許諾付きの dmg でも止まらないよう、入力を閉じて読み取り専用で開く
# Mount read-only with stdin closed so a license-bearing dmg cannot block
mkdir -p "$mount_path"
hdiutil attach "$dmg_path" -readonly -nobrowse -noautoopen -mountpoint "$mount_path" < /dev/null > /dev/null
ditto "$mount_path/Unity Hub.app" "$HUB_DESTINATION/Unity Hub.app"
hdiutil detach "$mount_path" -quiet

hub_exec="$HUB_DESTINATION/Unity Hub.app/Contents/MacOS/Unity Hub"
if [ ! -x "$hub_exec" ]; then
  echo "::error::Unity Hub executable was not installed at $hub_exec"
  exit 1
fi
echo "Installed Unity Hub ${HUB_VERSION}"
