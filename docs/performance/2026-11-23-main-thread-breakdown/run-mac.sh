#!/usr/bin/env bash
# Mac の起動済み Editor（GUI）でワールドを1回読み込み、決まった視点で、Profiler なしの fps 窓・スレッドごとの時間の窓・メインスレッド全体の階層の窓・VContainer の Tick ごとの窓を REPEAT 周取る
# usage: WORLD_SRC=<save.json・map.json・world.json のあるフォルダ> [REPEAT=3] run-mac.sh <label>  結果は results/<日時>_<label>_<コミット>.json
# 前提: Editor が起動済み・計測したいブランチでコンパイル済み。計測中は Editor を前面に置き、他のウィンドウを前に出さない
set -euo pipefail
LABEL="${1:?usage: run-mac.sh <label>}"
: "${WORLD_SRC:?WORLD_SRC にワールドのフォルダを指定する}"
REPEAT="${REPEAT:-3}"
WINDOW_SECONDS="${WINDOW_SECONDS:-6}"
DIR="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$DIR/../../.." && pwd)"
PROJ="$REPO/moorestech_client"
SN="$DIR/snippets"
BENCH="$DIR/../benchmark/snippets"
WORK="$(mktemp -d)"
mkdir -p "$DIR/results"

run() { # $1=スニペットのパス -> Result を出す
  uloop execute-dynamic-code --code-file "$1" --project-path "$PROJ" 2>&1 | python3 -c '
import json,sys
t=sys.stdin.read(); s=t.find("{")
d=json.loads(t[s:]) if s>=0 else {}
if not d.get("Success"): sys.exit("FAIL "+sys.argv[1]+": "+t[:1500])
print(d.get("Result",""))' "$1"
}
teardown() {
  run "$SN/mac-cleanup.cs" >/dev/null 2>&1 || true
  uloop control-play-mode --action Stop --project-path "$PROJ" >/dev/null 2>&1 || true
  run "$SN/frame-timing-off.cs" >/dev/null 2>&1 || true
  kill "$MEMPID" 2>/dev/null || true
  rm -rf "$WORK"
}
trap 'echo "ERROR: 途中で失敗しました" >&2; teardown' ERR

# このワールドで Profiler を記録し続けると Mac のスワップが数十GB に膨らむので、空き容量を見ながら測る
free=$(df -g /System/Volumes/Data | tail -1 | awk '{print $4}')
[ "$free" -ge 30 ] || { echo "ERROR: 空き容量 ${free}GB < 30GB" >&2; exit 4; }
STAMP="$(date +%Y-%m-%dT%H%M)"
"$DIR/memlog.sh" "$DIR/results/${STAMP}_${LABEL}_mem.log" & MEMPID=$!

# 遊んで状態が変わらないよう、毎回複製してから読み込む
rm -rf /tmp/moorestech-perf-world && mkdir -p /tmp/moorestech-perf-world
cp "$WORLD_SRC"/save.json "$WORLD_SRC"/map.json "$WORLD_SRC"/world.json /tmp/moorestech-perf-world/
WORLD_SHA="$(shasum -a 256 /tmp/moorestech-perf-world/save.json | cut -c1-16)"
COMMIT="$(git -C "$REPO" rev-parse HEAD)"
DIRTY="$(git -C "$REPO" status --porcelain --untracked-files=no | sed '/^$/d' | wc -l | tr -d ' ')"

uloop control-play-mode --action Stop --project-path "$PROJ" >/dev/null 2>&1 || true
sleep 3
run "$SN/profiler-off.cs"
run "$SN/frame-timing-on.cs"
uloop focus-window --project-path "$PROJ" >/dev/null 2>&1 || true
uloop control-play-mode --action Play --project-path "$PROJ" >/dev/null
for _ in $(seq 1 40); do r="$(run "$SN/mac-enter-world.cs" || true)"; case "$r" in started*) break ;; esac; sleep 3; done
case "$r" in started*) echo "$r" ;; *) echo "ERROR: ローカル開始できませんでした: $r" >&2; teardown; exit 3 ;; esac

# 定常（Renderer 数の変化が5秒で0.1%以内）を待つ
prev=-1; stable=0
for _ in $(seq 1 120); do
  sleep 5
  cur="$(run "$BENCH/scene-stats.cs" | python3 -c 'import json,sys
try: print(json.load(sys.stdin)["renderers"])
except Exception: print(-1)')"
  echo "  renderers=$cur"
  if [ "$cur" -gt 1000 ] && [ "$prev" -gt 0 ] && [ $(( (cur > prev ? cur - prev : prev - cur) * 1000 )) -le "$cur" ]; then stable=1; break; fi
  prev="$cur"
done
[ "$stable" = 1 ] || { echo "ERROR: 定常状態になりませんでした" >&2; teardown; exit 3; }
run "$SN/profiler-off.cs" >/dev/null
run "$BENCH/place-player.cs" > "$WORK/place.txt"; sleep 20
run "$BENCH/place-player.cs" > "$WORK/place.txt"; sleep 5
uloop focus-window --project-path "$PROJ" >/dev/null 2>&1 || true
cat "$WORK/place.txt"
run "$SN/mac-environment.cs" > "$WORK/environment.json"
run "$BENCH/scene-stats.cs" > "$WORK/scene.json"

# 1周 = fps 窓（Profiler なし）→ スレッドごとの時間の窓（ProfilerRecorder と FrameTimingManager）→ メインスレッドの階層の窓（Profiler は短く保つ）→ VContainer の Tick ごとの窓
for i in $(seq 1 "$REPEAT"); do
  echo "[maintree] 周 $i/$REPEAT"
  run "$BENCH/fps-start.cs" >/dev/null; sleep 10; run "$BENCH/fps-collect.cs" | tee "$WORK/fps_$i.json"
  run "$SN/render-start.cs" >/dev/null; sleep "$WINDOW_SECONDS"; run "$SN/render-collect.cs" > "$WORK/render_$i.json"
  run "$BENCH/profiler-start.cs" >/dev/null; sleep 4; run "$SN/maintree-collect.cs" > "$WORK/maintree_$i.json"
  head -c 200 "$WORK/maintree_$i.json"; echo
  # VContainer の Tick を1つずつ測る窓（Profiler なし。ラッパーを挟む上乗せがある）
  run "$SN/tick-probe-start.cs"; sleep "$WINDOW_SECONDS"; run "$SN/tick-probe-collect.cs" > "$WORK/tick_$i.json"
  head -c 300 "$WORK/tick_$i.json"; echo
done

OUT="$DIR/results/${STAMP}_${LABEL}_${COMMIT:0:10}.json"
python3 - "$WORK" "$OUT" "$REPEAT" "$LABEL" "$COMMIT" "$DIRTY" "$WORLD_SHA" <<'EOF'
import json, sys, os
work, out, rep, label, commit, dirty, sha = sys.argv[1], sys.argv[2], int(sys.argv[3]), sys.argv[4], sys.argv[5], int(sys.argv[6]), sys.argv[7]
load = lambda n: json.load(open(os.path.join(work, n + ".json")))
json.dump({
    "label": label, "commit": commit, "trackedChanges": dirty, "worldSaveSha256Prefix": sha,
    "environment": load("environment"), "scene": load("scene"),
    "playerSpot": open(os.path.join(work, "place.txt")).read().strip(),
    "fps": [load("fps_%d" % i) for i in range(1, rep + 1)],
    "render": [load("render_%d" % i) for i in range(1, rep + 1)],
    "maintree": [load("maintree_%d" % i) for i in range(1, rep + 1)],
    "ticks": [load("tick_%d" % i) for i in range(1, rep + 1)],
}, open(out, "w"), ensure_ascii=False, indent=2)
print("[maintree] 保存: " + out)
EOF
teardown
