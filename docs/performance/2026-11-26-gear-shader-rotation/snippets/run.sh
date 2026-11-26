#!/usr/bin/env bash
# 歯車のシェーダー回転の確認: ワールドを読み込み、計測位置で、歯車の状態・GPUで回っているか・fps・Tick ごとの時間を取る
# usage: BENCH_HOST=<ssh先> [MEASURE=1] [BASELINE_RUN=1] [OUTDIR=...] run.sh  前提: Editor 起動済み・MainMenu シーン・計測したいコミットでコンパイル済み
set -euo pipefail
HOST="${BENCH_HOST:?BENCH_HOST に計測機の ssh 先を指定する}"
PROJECT='G:\GitHub\moorestech\moorestech_client'
SN='C:\moorestech-perf\snippets'
DIR="$(cd "$(dirname "$0")" && pwd)"  # このフォルダの snippets
DOCS="$DIR/../.."
OUTDIR="${OUTDIR:-$DIR/out}"; mkdir -p "$OUTDIR"
SSH_OPTS=(-p 22 -o ConnectTimeout=10 -o ServerAliveInterval=10 -o ControlMaster=auto -o "ControlPath=/tmp/gpugear-ssh-%C" -o ControlPersist=120)
remote() { ssh "${SSH_OPTS[@]}" "$HOST" "$@"; }
run() {
  remote "uloop execute-dynamic-code --code-file $SN\\$1 --project-path $PROJECT" 2>&1 | python3 -c '
import json,sys
t=sys.stdin.read(); s=t.find("{")
d=json.loads(t[s:],strict=False) if s>=0 else {}
if not d.get("Success"): sys.exit("FAIL "+sys.argv[1]+": "+t[:1500])
print(d.get("Result",""))' "$1"
}
scp -q -P 22 -o "ControlPath=/tmp/gpugear-ssh-%C" "$DOCS"/benchmark/snippets/*.cs "$DOCS"/2026-11-21-draw-call-breakdown/snippets/capture.cs "$DOCS"/2026-11-21-draw-call-breakdown/snippets/profiler-off.cs $( [ "${BASELINE_RUN:-0}" = 1 ] && ls "$DIR"/tick-probe-*.cs || ls "$DIR"/*.cs ) "$HOST:C:/moorestech-perf/snippets/"
remote "uloop control-play-mode --action Stop --project-path $PROJECT" >/dev/null 2>&1 || true
run profiler-off.cs
remote "\$b='C:\\moorestech-perf\\baselines\\2026-10-22-world_1'; \$w='C:\\moorestech-perf\\work\\current'; if (Test-Path \$w) { Remove-Item \$w -Recurse -Force }; New-Item -ItemType Directory -Force -Path \$w | Out-Null; Copy-Item \"\$b\\*\" \$w -Force"
remote "uloop control-play-mode --action Play --project-path $PROJECT" >/dev/null
for _ in $(seq 1 40); do r="$(run enter-world.cs || true)"; case "$r" in started*) break ;; esac; sleep 3; done
echo "$r"
prev=-1
for _ in $(seq 1 120); do
  sleep 5
  cur="$(run scene-stats.cs | python3 -c 'import json,sys
try: print(json.load(sys.stdin)["renderers"])
except Exception: print(-1)')"
  echo "  renderers=$cur"
  if [ "$cur" -gt 1000 ] && [ "$prev" -gt 0 ] && [ $(( (cur > prev ? cur - prev : prev - cur) * 1000 )) -le "$cur" ]; then break; fi
  prev="$cur"
done
run place-player.cs; sleep 20; run place-player.cs; sleep 5
if [ "${BASELINE_RUN:-0}" != 1 ]; then
run gear-gpu-check.cs | tee "$OUTDIR/check.txt"
run gear-gpu-near.cs | tee "$OUTDIR/near.txt"
run gear-gpu-render.cs | tee "$OUTDIR/render.txt"
for f in gear-a gear-b gear-a2; do scp -q -P 22 -o "ControlPath=/tmp/gpugear-ssh-%C" "$HOST:C:/moorestech-perf/$f.png" "$OUTDIR/$f.png"; done
fi
for k in; do run capture.cs >/dev/null; sleep 3; scp -q -P 22 -o "ControlPath=/tmp/gpugear-ssh-%C" "$HOST:C:/moorestech-perf/shot.png" "$OUTDIR/near$k.png"; done
if [ "${MEASURE:-0}" = 1 ]; then for k in 1 2; do
  run fps-start.cs >/dev/null; sleep 10; run fps-collect.cs | tee -a "$OUTDIR/fps.txt"
  run tick-probe-start.cs >/dev/null; sleep 6; run tick-probe-collect.cs | tee -a "$OUTDIR/ticks.txt"
done; fi
remote "uloop get-logs --project-path $PROJECT --log-type Error" > "$OUTDIR/errors.txt" 2>&1 || true
run cleanup.cs >/dev/null 2>&1 || true
remote "uloop control-play-mode --action Stop --project-path $PROJECT" >/dev/null 2>&1 || true
echo done
