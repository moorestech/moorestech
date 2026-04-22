#!/usr/bin/env bash
# moorestech_web セットアップ: Node.js LTS と pnpm スタンドアロンバイナリを
# moorestech_web/node/<platform>/ にダウンロードする。
# Setup for moorestech_web: downloads Node.js LTS and pnpm standalone
# binaries into moorestech_web/node/<platform>/.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NODE_VERSION="20.18.1"
PNPM_VERSION="9.15.0"

uname_os="$(uname -s)"
uname_arch="$(uname -m)"

case "$uname_os-$uname_arch" in
  Darwin-arm64) PLATFORM="mac-arm64"; NODE_PLATFORM="darwin-arm64"; PNPM_SUFFIX="macos-arm64"; NODE_EXT="tar.gz";;
  Darwin-x86_64) PLATFORM="mac-x64"; NODE_PLATFORM="darwin-x64"; PNPM_SUFFIX="macos-x64"; NODE_EXT="tar.gz";;
  Linux-x86_64) PLATFORM="linux-x64"; NODE_PLATFORM="linux-x64"; PNPM_SUFFIX="linux-x64"; NODE_EXT="tar.xz";;
  *) echo "Unsupported platform: $uname_os-$uname_arch"; exit 1;;
esac

TARGET_DIR="$SCRIPT_DIR/node/$PLATFORM"
mkdir -p "$TARGET_DIR"

NODE_URL="https://nodejs.org/dist/v${NODE_VERSION}/node-v${NODE_VERSION}-${NODE_PLATFORM}.${NODE_EXT}"
PNPM_URL="https://github.com/pnpm/pnpm/releases/download/v${PNPM_VERSION}/pnpm-${PNPM_SUFFIX}"

echo "[setup] downloading node from $NODE_URL"
curl -fL "$NODE_URL" -o "/tmp/node.$NODE_EXT"
if [ "$NODE_EXT" = "tar.xz" ]; then
  tar -xJf "/tmp/node.$NODE_EXT" -C "$TARGET_DIR" --strip-components=1
else
  tar -xzf "/tmp/node.$NODE_EXT" -C "$TARGET_DIR" --strip-components=1
fi
rm "/tmp/node.$NODE_EXT"

echo "[setup] downloading pnpm from $PNPM_URL"
curl -fL "$PNPM_URL" -o "$TARGET_DIR/pnpm"
chmod +x "$TARGET_DIR/pnpm"

echo "[setup] done. node: $TARGET_DIR/bin/node, pnpm: $TARGET_DIR/pnpm"
