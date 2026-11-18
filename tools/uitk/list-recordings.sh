#!/usr/bin/env bash
# 全録画の一覧を作る。切替後は描く系が UITK だけなので、scenarios 配下の全 .cs を流す
# Lists every recording; after the cutover UITK is the only drawing system, so every .cs under scenarios runs
set -euo pipefail
if [ $# -ne 0 ]; then
  echo "usage: $0" >&2
  exit 2
fi
root=.agents/skills/unity-playmode-recorded-playtest/scenarios
find "$root" -name '*.cs' | sort
