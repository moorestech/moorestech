#!/usr/bin/env bash
# NOTE: 方式B(レガシー手動フロー)用の参考実装。DSLがあるブランチでは PlaytestRunOptions{Record=true} が録画を内蔵するのでこのスクリプトは不要。
# Stop Unity Recorder previously started by start-recording.sh.
# Usage: stop-recording.sh --project-path <unity-project>
set -euo pipefail

PROJECT=""

usage() {
  cat <<EOF
Usage: $(basename "$0") --project-path <unity-project>

Retrieves RecorderController from AppDomain key "playtest_recorder" and stops it.
The MP4 file path was set when start-recording.sh was called.
EOF
  exit 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --project-path) PROJECT="$2"; shift 2;;
    -h|--help) usage;;
    *) echo "unknown arg: $1" >&2; usage;;
  esac
done

[[ -z "$PROJECT" ]] && usage

uloop execute-dynamic-code --project-path "$PROJECT" --code '
using System;
using UnityEditor.Recorder;
var ctrl = AppDomain.CurrentDomain.GetData("playtest_recorder") as RecorderController;
if (ctrl == null) return "ERROR: no recorder in AppDomain (key=playtest_recorder)";
var was = ctrl.IsRecording();
ctrl.StopRecording();
AppDomain.CurrentDomain.SetData("playtest_recorder", null);
return $"stopped (was recording: {was})";
'
