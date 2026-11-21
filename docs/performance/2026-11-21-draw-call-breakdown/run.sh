#!/usr/bin/env bash
# windows-desktop でベースラインワールドを1回読み込み、決まった視点で、描画のパスを1つずつ切った窓の描画カウンタと、Profiler なしの fps・Profiler の窓を取る
# スキンメッシュは切らない（グラフィックスジョブなし -force-gfx-mt の Editor で測る前提。6000.3.8f1 の Split では GPU が止まる）
# usage: [REPEAT=2] [SHOT=1] run.sh <label>  結果は results/<日時>_<label>_<コミット>.json（SHOT=1 なら画面の PNG も）
# 前提: Editor が起動済み・計測したいブランチを checkout してコンパイル済み
set -euo pipefail
LABEL="${1:?usage: run.sh <label>}"
BASELINE="${BASELINE:-2026-10-22-world_1}"
HOST="${BENCH_HOST:-windows-desktop}"
PROJECT='G:\GitHub\moorestech\moorestech_client'
REPO='G:\GitHub\moorestech'
SN='C:\moorestech-perf\snippets'
REPEAT="${REPEAT:-2}"
WINDOW_SECONDS="${WINDOW_SECONDS:-6}"
CONDS="${CONDS:-base shadows ssao outlines camtex post}"
DIR="$(cd "$(dirname "$0")" && pwd)"
BENCH="$DIR/../benchmark/snippets"
WORK="$(mktemp -d)"
SSH_OPTS=(-o ConnectTimeout=10 -o ServerAliveInterval=10 -o ServerAliveCountMax=3 -o ControlMaster=auto -o "ControlPath=/tmp/drawcall-ssh-%C" -o ControlPersist=120)
trap 'ssh "${SSH_OPTS[@]}" -O exit "$HOST" >/dev/null 2>&1; rm -rf "$WORK"' EXIT
remote() { ssh "${SSH_OPTS[@]}" "$HOST" "$@"; }
run() {
  remote "uloop execute-dynamic-code --code-file $SN\\$1 --project-path $PROJECT" 2>&1 | python3 -c '
import json,sys
t=sys.stdin.read(); s=t.find("{")
d=json.loads(t[s:]) if s>=0 else {}
if not d.get("Success"): sys.exit("FAIL "+sys.argv[1]+": "+t[:1500])
print(d.get("Result",""))' "$1"
}
teardown() {
  run cleanup.cs >/dev/null 2>&1 || true
  remote "uloop control-play-mode --action Stop --project-path $PROJECT" >/dev/null 2>&1 || true
  run frame-timing-off.cs >/dev/null 2>&1 || true
}
trap 'echo "ERROR: 途中で失敗しました" >&2; teardown' ERR

# 条件ごとに COND 行を書き換えた pass-toggle の写しを作って送る
for c in $CONDS; do sed "s/^var COND = \"base\";/var COND = \"$c\";/" "$DIR/snippets/pass-toggle.cs" > "$WORK/pass-$c.cs"; done
remote "New-Item -ItemType Directory -Force -Path $SN | Out-Null"
scp -q "${SSH_OPTS[@]}" "$BENCH"/*.cs "$DIR"/snippets/render-*.cs "$DIR"/snippets/grd-check.cs "$DIR"/snippets/capture.cs "$DIR"/snippets/camtree-collect.cs "$DIR"/snippets/frame-timing-*.cs "$WORK"/pass-*.cs "$HOST:C:/moorestech-perf/snippets/"

COMMIT="$(remote "git -C $REPO rev-parse HEAD" | tr -d '\r')"
DIRTY="$(remote "git -C $REPO status --porcelain --untracked-files=no" | tr -d '\r' | sed '/^$/d' | wc -l | tr -d ' ')"
remote "uloop control-play-mode --action Stop --project-path $PROJECT" >/dev/null 2>&1 || true
remote "\$b='C:\\moorestech-perf\\baselines\\$BASELINE'; \$w='C:\\moorestech-perf\\work\\current'; if (Test-Path \$w) { Remove-Item \$w -Recurse -Force }; New-Item -ItemType Directory -Force -Path \$w | Out-Null; Copy-Item \"\$b\\*\" \$w -Force"
# GPU のフレーム時間を取るため、Play 前に FrameTimingManager を有効にする（後片付けで戻す）
run frame-timing-on.cs
remote "uloop control-play-mode --action Play --project-path $PROJECT" >/dev/null
for _ in $(seq 1 40); do r="$(run enter-world.cs || true)"; case "$r" in started*) break ;; esac; sleep 3; done
case "$r" in started*) echo "$r" ;; *) echo "ERROR: ローカル開始できませんでした: $r" >&2; teardown; exit 3 ;; esac

# 定常（Renderer 数の変化が5秒で0.1%以内）を待つ
prev=-1; stable=0
for _ in $(seq 1 120); do
  sleep 5
  cur="$(run scene-stats.cs | python3 -c 'import json,sys
try: print(json.load(sys.stdin)["renderers"])
except Exception: print(-1)')"
  echo "  renderers=$cur"
  if [ "$cur" -gt 1000 ] && [ "$prev" -gt 0 ] && [ $(( (cur > prev ? cur - prev : prev - cur) * 1000 )) -le "$cur" ]; then stable=1; break; fi
  prev="$cur"
done
[ "$stable" = 1 ] || { echo "ERROR: 定常状態になりませんでした" >&2; teardown; exit 3; }
run place-player.cs > "$WORK/place.txt"; sleep 20
run place-player.cs > "$WORK/place.txt"; sleep 5
run environment.cs > "$WORK/environment.json"
run scene-stats.cs > "$WORK/scene.json"
run grd-check.cs | tee "$WORK/grd.txt"

# 1周 = fps 窓（Profiler なし）→ Profiler 窓 → 条件ごとの描画カウンタ窓（切り替えてから4秒待つ）
echo "[" > "$WORK/windows.json"; first=1
for i in $(seq 1 "$REPEAT"); do
  echo "[drawcall] 周 $i/$REPEAT"
  run pass-base.cs >/dev/null; sleep 4
  run fps-start.cs >/dev/null; sleep 10; run fps-collect.cs > "$WORK/fps_$i.json"
  run profiler-start.cs >/dev/null; sleep "$WINDOW_SECONDS"; run profiler-collect.cs > "$WORK/profiler_$i.json"
  # カメラ描画の中身（メインスレッドの階層と、ほかのスレッドのマーカー）。Profiler は短く保つ
  run profiler-start.cs >/dev/null; sleep 4; run camtree-collect.cs > "$WORK/camtree_$i.json"
  for c in $CONDS base; do
    state="$(run "pass-$c.cs")"; sleep 4
    run render-start.cs >/dev/null; sleep "$WINDOW_SECONDS"
    res="$(run render-collect.cs)"
    echo "  $c: $res" | cut -c1-260
    [ $first = 1 ] || echo "," >> "$WORK/windows.json"; first=0
    python3 -c 'import json,sys; print(json.dumps({"cycle":int(sys.argv[1]),"cond":sys.argv[2],"state":sys.argv[3],"render":json.loads(sys.argv[4])},ensure_ascii=False))' "$i" "$c" "$state" "$res" >> "$WORK/windows.json"
  done
done
echo "]" >> "$WORK/windows.json"
run pass-base.cs >/dev/null
# SHOT=1 なら、全部の条件を戻した状態のゲーム画面を results/<日時>_<label>.png に保存する
if [ "${SHOT:-0}" = 1 ]; then
  sleep 4; run capture.cs >/dev/null; sleep 5
  scp -q "${SSH_OPTS[@]}" "$HOST:C:/moorestech-perf/shot.png" "$DIR/results/$(date +%Y-%m-%dT%H%M)_${LABEL}.png"
fi

OUT="$DIR/results/$(date +%Y-%m-%dT%H%M)_${LABEL}_${COMMIT:0:10}.json"
python3 - "$WORK" "$OUT" "$REPEAT" <<EOF
import json, sys, os
work, out, rep = sys.argv[1], sys.argv[2], int(sys.argv[3])
load = lambda n: json.load(open(os.path.join(work, n + ".json")))
json.dump({
    "label": "$LABEL", "commit": "$COMMIT", "trackedChangesOnHost": int("$DIRTY"), "baselineWorld": "$BASELINE",
    "environment": load("environment"), "scene": load("scene"),
    "playerSpot": open(os.path.join(work, "place.txt")).read().strip(),
    "grdCheck": open(os.path.join(work, "grd.txt")).read().strip(),
    "fps": [load("fps_%d" % i) for i in range(1, rep + 1)],
    "profiler": [load("profiler_%d" % i) for i in range(1, rep + 1)],
    "camtree": [load("camtree_%d" % i) for i in range(1, rep + 1)],
    "windows": load("windows"),
}, open(out, "w"), ensure_ascii=False, indent=2)
print("[drawcall] 保存: " + out)
EOF
teardown
