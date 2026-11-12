#!/usr/bin/env bash
# windows-desktop でベースラインワールドを読み込み、定常後に Transform 内訳と設置モードに入る瞬間（3回）を測る
# usage: run-buildmode-entry.sh <label>  結果は results/<日時>_<label>_buildmode.txt
set -euo pipefail
LABEL="${1:?usage: run-buildmode-entry.sh <label>}"
HOST="${BENCH_HOST:-windows-desktop}"
PROJECT='G:\GitHub\moorestech\moorestech_client'
SN='C:\moorestech-perf\snippets'
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SSH_OPTS=(-o ConnectTimeout=10 -o ServerAliveInterval=10 -o ServerAliveCountMax=3)
remote() { ssh "${SSH_OPTS[@]}" "$HOST" "$@"; }
run() {
  remote "uloop execute-dynamic-code --code-file $SN\\$1 --project-path $PROJECT" 2>&1 | python3 -c '
import json,sys
t=sys.stdin.read(); s=t.find("{")
d=json.loads(t[s:]) if s>=0 else {}
print(d.get("Result","") if d.get("Success") else "FAIL "+t[:800])'
}
# 途中で失敗しても作業ワールドの差し替えを残さないよう、後片付けしてから止める
# Even on failure, clear the work-world override and stop PlayMode before exiting
trap 'echo "ERROR: 途中で失敗しました（$OUT）" >&2; run cleanup.cs >/dev/null || true; remote "uloop control-play-mode --action Stop --project-path $PROJECT" >/dev/null 2>&1 || true' ERR
OUT="$SCRIPT_DIR/results/$(date +%Y-%m-%dT%H%M)_${LABEL}_buildmode.txt"
remote "New-Item -ItemType Directory -Force -Path $SN | Out-Null"
scp -q "${SSH_OPTS[@]}" "$SCRIPT_DIR"/snippets/*.cs "$SCRIPT_DIR"/../2026-10-22-gameobject-count/go-breakdown.cs "$HOST:C:/moorestech-perf/snippets/"
echo "commit=$(remote "git -C G:\\GitHub\\moorestech rev-parse HEAD" | tr -d '\r')" > "$OUT"
remote "uloop control-play-mode --action Stop --project-path $PROJECT" >/dev/null 2>&1 || true
remote "\$b='C:\\moorestech-perf\\baselines\\2026-10-22-world_1'; \$w='C:\\moorestech-perf\\work\\current'; if (Test-Path \$w) { Remove-Item \$w -Recurse -Force }; New-Item -ItemType Directory -Force -Path \$w | Out-Null; Copy-Item \"\$b\\*\" \$w -Force"
remote "uloop control-play-mode --action Play --project-path $PROJECT" >/dev/null
for _ in $(seq 1 40); do r="$(run enter-world.cs)"; case "$r" in started*) break ;; esac; sleep 3; done
case "$r" in started*) ;; *) echo "ERROR: ローカル開始できませんでした: $r" >&2; run cleanup.cs; exit 3 ;; esac
# 定常（Renderer 数の変化が5秒で0.1%以内）を待ってから20秒置く
# Wait for the steady state (renderer count within 0.1% over 5 s), then settle 20 s
prev=-1; stable=0
for _ in $(seq 1 120); do
  sleep 5
  cur="$(run scene-stats.cs | python3 -c 'import json,sys
try: print(json.load(sys.stdin)["renderers"])
except Exception: print(-1)')"
  if [ "$cur" -gt 1000 ] && [ "$prev" -gt 0 ] && [ $(( (cur > prev ? cur - prev : prev - cur) * 1000 )) -le "$cur" ]; then stable=1; break; fi
  prev="$cur"
done
[ "$stable" = 1 ] || { echo "ERROR: 定常状態になりませんでした" >&2; run cleanup.cs; exit 3; }
sleep 20
echo "== go-breakdown" >> "$OUT"; run go-breakdown.cs >> "$OUT"
# Get-Content は SSH 越しに日本語のブロック名が化けるので、UTF-8 のファイルのまま取り込む
# Get-Content mangles Japanese block names over SSH, so copy the UTF-8 file as is
scp -q "${SSH_OPTS[@]}" "$HOST:C:/moorestech-perf/go-breakdown.txt" "$OUT.go-breakdown" && cat "$OUT.go-breakdown" >> "$OUT" && rm -f "$OUT.go-breakdown"
# 設置プレビュー枠と当たり判定の数（ブロック配下かどうか）を、設置モードに入る前と入っている間に数える
# Count preview frames and hit faces (under blocks or not) before and during build mode
echo "== placement-frame-count steady" >> "$OUT"; run placement-frame-count.cs >> "$OUT"
for i in 1 2 3; do
  echo "== buildmode $i" >> "$OUT"
  run buildmode-enter.cs >> "$OUT"; sleep 3
  # 止めた直後の同じ呼び出しでは記録が読めないことがあるので、止めてから2秒待って集計する
  # Frames may be unreadable in the same call that stops recording, so stop first and collect 2 s later
  run profiler-stop.cs >> "$OUT"; sleep 2
  run buildmode-frames-collect.cs >> "$OUT"
  echo "-- placement-frame-count in-buildmode" >> "$OUT"; run placement-frame-count.cs >> "$OUT"
  run buildmode-exit.cs >> "$OUT"; sleep 3
done
run cleanup.cs >/dev/null
remote "uloop control-play-mode --action Stop --project-path $PROJECT" >/dev/null 2>&1 || true
echo "[buildmode] 保存: $OUT"
