#!/usr/bin/env bash
# ワールド読み込み時間の再計測（windows-desktop の Editor が起動・コンパイル済みであること）
# usage: load-measure.sh <timeline|heavy|subtree> <出力先ディレクトリ>
#   timeline … ログ・fps・Renderer数の時系列（Profiler なし）
#   heavy    … Profiler で1秒超フレームの上位階層
#   subtree  … Profiler で1秒超フレームの主要な塊の配下を名前ごとの self 時間で合算
set -euo pipefail
MODE="${1:?timeline|heavy|subtree}"
OUTDIR="${2:?outdir}"
HOST=windows-desktop
PROJECT='G:\GitHub\moorestech\moorestech_client'
SN='C:\moorestech-perf\snippets'
# スニペットはこのスクリプトと同じチェックアウトから送る（別 worktree の古い版を送らないため）
# Send snippets from the same checkout as this script (not an older copy in another worktree)
HERE="$(cd "$(dirname "$0")" && pwd)"
LT="$HERE/../2026-10-22-world-load-time"
BS="$HERE/../benchmark/snippets"
SSH_OPTS=(-o ConnectTimeout=10 -o ServerAliveInterval=10 -o ServerAliveCountMax=3)
remote() { ssh "${SSH_OPTS[@]}" "$HOST" "$@"; }
run() {
  remote "uloop execute-dynamic-code --code-file $SN\\$1 --project-path $PROJECT" 2>&1 | python3 -c '
import json,sys
t=sys.stdin.read(); s=t.find("{")
d=json.loads(t[s:]) if s>=0 else {}
print(d.get("Result","") if d.get("Success") else "FAIL "+t[:800])'
}
mkdir -p "$OUTDIR"
remote "New-Item -ItemType Directory -Force -Path $SN | Out-Null"
scp -q "${SSH_OPTS[@]}" "$BS"/*.cs "$LT"/tl-start.cs "$LT"/tl-collect.cs "$LT"/heavy-collect.cs "$(dirname "$0")"/subtree-collect.cs "$HOST:C:/moorestech-perf/snippets/"
remote "uloop control-play-mode --action Stop --project-path $PROJECT" >/dev/null 2>&1 || true
remote "\$b='C:\\moorestech-perf\\baselines\\2026-10-22-world_1'; \$w='C:\\moorestech-perf\\work\\current'; if (Test-Path \$w) { Remove-Item \$w -Recurse -Force }; New-Item -ItemType Directory -Force -Path \$w | Out-Null; Copy-Item \"\$b\\*\" \$w -Force"
remote "uloop control-play-mode --action Play --project-path $PROJECT" >/dev/null
sleep 5
# 開始ボタンを押す直前から記録する
if [ "$MODE" = timeline ]; then echo "$(run tl-start.cs)"; else echo "$(run profiler-start.cs)"; fi
for _ in $(seq 1 40); do
  r="$(run enter-world.cs)"
  case "$r" in started*) break ;; esac
  sleep 2
done
echo "enter: $r"
T0=$(date +%s)
prev=-1; stable=0
for _ in $(seq 1 60); do
  sleep 5
  cur="$(run scene-stats.cs | python3 -c 'import json,sys
try: print(json.load(sys.stdin)["renderers"])
except Exception: print(-1)')"
  echo "  $(( $(date +%s) - T0 ))s renderers=$cur"
  if [ "$cur" -gt 1000 ] && [ "$prev" -gt 0 ] && [ $(( (cur > prev ? cur - prev : prev - cur) * 1000 )) -le "$cur" ]; then stable=1; break; fi
  prev="$cur"
done
echo "stable=$stable elapsed=$(( $(date +%s) - T0 ))s"
if [ "$MODE" = timeline ]; then
  echo "$(run tl-collect.cs)"
  sleep 3; scp -q "${SSH_OPTS[@]}" "$HOST:C:/moorestech-perf/timeline.txt" "$OUTDIR/timeline-raw.txt" || true
else
  if [ "$MODE" = subtree ]; then echo "$(run subtree-collect.cs)"; else echo "$(run heavy-collect.cs)"; fi
  sleep 3; scp -q "${SSH_OPTS[@]}" "$HOST:C:/moorestech-perf/heavy-frames.txt" "$OUTDIR/heavy-frames-raw.txt" || true
fi
run cleanup.cs >/dev/null
remote "uloop control-play-mode --action Stop --project-path $PROJECT" >/dev/null 2>&1 || true
