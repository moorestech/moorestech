#!/usr/bin/env sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
REPOSITORY_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd)
PACKAGE_CACHE="$REPOSITORY_ROOT/moorestech_client/Library/PackageCache"

if ! command -v git >/dev/null 2>&1; then
    echo "ERROR: git is required. Install Git and run this script again." >&2
    exit 1
fi

if ! git lfs version >/dev/null 2>&1; then
    echo "ERROR: git-lfs is required. Install Git LFS and run this script again." >&2
    exit 1
fi

echo "Configuring the Git LFS smudge filter for UPM git dependencies..."
git lfs install

if [ -d "$PACKAGE_CACHE" ]; then
    echo "Removing cached jp.juha.cefunity packages..."
    find "$PACKAGE_CACHE" -maxdepth 1 -type d -name 'jp.juha.cefunity@*' -exec rm -rf {} +
fi

echo "CEF setup is ready. Open moorestech_client in Unity to let UPM resolve the package again."
