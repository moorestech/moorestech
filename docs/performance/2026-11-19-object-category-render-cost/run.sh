#!/usr/bin/env bash
# 起動済みの Editor（通常でも batchmode でも）で、ワールドに入り、決まった位置へ移して、種類ごとの描画コストを測る
# usage: WORLD_SRC=<save.json・map.json・world.json のあるフォルダ> run.sh <label>
# 結果は results/<label>.json。比べるときは analyze.py
set -euo pipefail
LABEL="${1:?label}"
DIR="$(cd "$(dirname "$0")" && pwd)"
PROJ="$(cd "$DIR/../../.." && pwd)/moorestech_client"
: "${WORLD_SRC:?WORLD_SRC にワールドのフォルダを指定する}"
# 遊んで状態が変わらないよう、毎回複製してから読み込む
rm -rf /tmp/moorestech-camprobe-world && mkdir -p /tmp/moorestech-camprobe-world
cp "$WORLD_SRC"/save.json "$WORLD_SRC"/map.json "$WORLD_SRC"/world.json /tmp/moorestech-camprobe-world/
SNIP="$DIR/../benchmark/snippets"
OUT="$DIR/results"; mkdir -p "$OUT"

snippet() { # $1=file -> prints Result
  local out
  out="$(uloop execute-dynamic-code --code-file "$1" --project-path "$PROJ" 2>&1)"
  python3 -c '
import json,sys
t=sys.stdin.read(); s=t.find("{")
if s<0: sys.exit("no json: "+t[:400])
d=json.loads(t[s:])
if not d.get("Success"): sys.exit("FAIL "+sys.argv[1]+": "+json.dumps({k:d.get(k) for k in ("ErrorMessage","Error","CompilationErrors")},ensure_ascii=False)[:2000])
print(d.get("Result",""))' "$1" <<<"$out"
}
code() { local f; f="$(mktemp).cs"; printf '%s' "$1" > "$f"; snippet "$f"; }

free=$(df -g /System/Volumes/Data | tail -1 | awk '{print $4}')
[ "$free" -ge 30 ] || { echo "free disk ${free}GB < 30GB; abort"; exit 4; }
"$DIR/memlog.sh" "$OUT/${LABEL}_mem.log" & MEMPID=$!
trap 'kill $MEMPID 2>/dev/null' EXIT
uloop control-play-mode --action Stop --project-path "$PROJ" >/dev/null 2>&1 || true
sleep 3
snippet "$DIR/snippets/profiler-off.cs"
code 'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Game/MainMenu.unity"); return "opened";'
uloop control-play-mode --action Play --project-path "$PROJ" >/dev/null
for _ in $(seq 1 40); do
  r="$(snippet "$DIR/snippets/mac-enter-world.cs" || true)"
  case "$r" in started*) echo "$r"; break ;; esac
  sleep 3
done
case "$r" in started*) ;; *) echo "enter failed: $r"; exit 3 ;; esac

# batchmode ならカメラを毎フレーム描かせる（MapObject の距離判定もカメラのカリングに頼るので早めに入れる）
for _ in $(seq 1 60); do
  r="$(code 'return Object.FindFirstObjectByType<Client.Game.InGame.Control.InGameCameraController>() != null ? "ingame" : "loading";' || true)"
  [ "$r" = "ingame" ] && break; sleep 5
done
snippet "$DIR/snippets/batch-render-on.cs"
snippet "$DIR/snippets/profiler-off.cs"

echo "settle: renderer count stable"
prev=-1
for _ in $(seq 1 120); do
  sleep 5
  cur="$(snippet "$SNIP/scene-stats.cs" | python3 -c 'import json,sys; print(json.load(sys.stdin)["renderers"])')"
  echo "  renderers=$cur"
  if [ "$prev" -gt 0 ] && python3 -c "import sys; sys.exit(0 if abs($cur-$prev)<=$prev*0.001 else 1)"; then break; fi
  prev=$cur
done
snippet "$DIR/snippets/place-player.cs"
sleep 20
snippet "$DIR/snippets/place-player.cs"
sleep 5
uloop focus-window --project-path "$PROJ" >/dev/null 2>&1 || true
code 'var q = Client.Game.InGame.GraphicsQuality.GraphicsQualityPreference.Current; return "farClip=" + Camera.main.farClipPlane + " drawDistance=" + q.DrawDistance + " shadows=" + q.ShadowsEnabled + " mapObjects=" + q.MapObjectsVisible + " cams=" + string.Join("/", System.Linq.Enumerable.Select(Camera.allCameras, c => c.name + ":" + c.enabled + ":" + (c.targetTexture ? c.targetTexture.name : "screen"))) + " lodBias=" + QualitySettings.lodBias;' | tee "$OUT/${LABEL}_quality.txt"
code 'return "batch=" + Application.isBatchMode + " opt=" + UnityEditor.Compilation.CompilationPipeline.codeOptimization + " gfx=" + SystemInfo.graphicsDeviceType + " cam=" + Camera.main.pixelWidth + "x" + Camera.main.pixelHeight + " vsync=" + QualitySettings.vSyncCount + " target=" + Application.targetFrameRate + " quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()];' | tee "$OUT/${LABEL}_env.txt"
snippet "$DIR/snippets/camprobe-start.cs"
while true; do
  sleep 15
  r="$(snippet "$DIR/snippets/camprobe-collect.cs")"
  case "$r" in *'"running":true'*) echo "  $r" ;; *) printf '%s' "$r" > "$OUT/${LABEL}.json"; echo "saved $OUT/${LABEL}.json"; break ;; esac
done
uloop control-play-mode --action Stop --project-path "$PROJ" >/dev/null 2>&1 || true
