#!/usr/bin/env sh
set -eu

# captureと解析を同じ入力manifestから再現する
# Reproduce capture and analysis from one generated input manifest
repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/../../../.." && pwd)
webui_directory="$repository_root/moorestech_web/webui"
raw_manifest=/tmp/task6-grip-frontier-raw.tsv
output_manifest="$repository_root/.superpowers/sdd/2026-07-30-craft-tab-corner-parity/measure/task-6-grip-frontier-manifest.tsv"
analysis_python=${WEBUI_CRAFT_PYTHON:-/tmp/webui-craft-qa-venv/bin/python}

cd "$webui_directory"
pnpm build
pnpm exec tsx "$repository_root/.superpowers/sdd/2026-07-30-craft-tab-corner-parity/measure/capture_grip_frontier.ts" "$raw_manifest"
"$analysis_python" "$repository_root/.superpowers/sdd/2026-07-30-craft-tab-corner-parity/measure/measure_grip_frontier.py" --manifest "$raw_manifest" --output "$output_manifest"
