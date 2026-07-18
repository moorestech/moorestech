#!/usr/bin/env bash
# NOTE: 方式B(レガシー手動フロー)用の参考実装。DSLがあるブランチでは PlaytestRunOptions{Record=true} が録画を内蔵するのでこのスクリプトは不要。
# Start Unity Recorder via uloop execute-dynamic-code, store controller in AppDomain.
# Usage: start-recording.sh --project-path <unity-project> --output <path-without-ext> [--width 1280] [--height 720] [--fps 30]
set -euo pipefail

PROJECT=""
OUTPUT=""
WIDTH=1280
HEIGHT=720
FPS=30

usage() {
  cat <<EOF
Usage: $(basename "$0") --project-path <unity-project> --output <path-without-ext> [--width N] [--height N] [--fps N]

Starts Unity Recorder in Manual mode and writes <path-without-ext>.mp4 when stop-recording.sh is called.
RecorderController is persisted in AppDomain under key "playtest_recorder".
Requires: Unity Editor running with CLI Loop server started, PlayMode active.
EOF
  exit 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --project-path) PROJECT="$2"; shift 2;;
    --output) OUTPUT="$2"; shift 2;;
    --width) WIDTH="$2"; shift 2;;
    --height) HEIGHT="$2"; shift 2;;
    --fps) FPS="$2"; shift 2;;
    -h|--help) usage;;
    *) echo "unknown arg: $1" >&2; usage;;
  esac
done

[[ -z "$PROJECT" || -z "$OUTPUT" ]] && usage

uloop execute-dynamic-code --project-path "$PROJECT" --code "
using System;
using UnityEngine;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;

var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
settings.SetRecordModeToManual();
settings.FrameRate = ${FPS};

var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
movie.name = \"playtest\";
movie.Enabled = true;
movie.EncoderSettings = new CoreEncoderSettings {
    EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
    Codec = CoreEncoderSettings.OutputCodec.MP4,
};
movie.ImageInputSettings = new GameViewInputSettings {
    OutputWidth = ${WIDTH},
    OutputHeight = ${HEIGHT},
};
movie.OutputFile = \"${OUTPUT}\";

settings.AddRecorderSettings(movie);
var ctrl = new RecorderController(settings);
ctrl.PrepareRecording();
ctrl.StartRecording();
AppDomain.CurrentDomain.SetData(\"playtest_recorder\", ctrl);
return \"recording: ${OUTPUT}.mp4 isRec=\" + ctrl.IsRecording();
"
