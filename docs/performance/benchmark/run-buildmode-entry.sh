#!/usr/bin/env bash
# windows-desktop でベースラインワールドを1回読み込み、定常後に Transform 内訳と、設置モードに入る瞬間（出入り4回＋最後にもう1回入る）を測る
# 遷移は buildmode-probe-start.cs が PlayerLoop の中で起こす（スニペット実行の停止で溜まった受信イベントが落ち着いてから）。詳細は README
# usage: run-buildmode-entry.sh <label>  結果は results/<日時>_<label>_buildmode.txt
# Load the baseline world once on windows-desktop; after the steady state, take the Transform breakdown and the build-mode entry probe (4 enter/exit cycles plus a final enter)
set -euo pipefail
LABEL="${1:?usage: run-buildmode-entry.sh <label>}"
HOST="${BENCH_HOST:-windows-desktop}"
PROJECT='G:\GitHub\moorestech\moorestech_client'
SN='C:\moorestech-perf\snippets'
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SSH_OPTS=(-o ConnectTimeout=10 -o ServerAliveInterval=10 -o ServerAliveCountMax=3)
OUT="$SCRIPT_DIR/results/$(date +%Y-%m-%dT%H%M)_${LABEL}_buildmode.txt"
remote() { ssh "${SSH_OPTS[@]}" "$HOST" "$@"; }
run() {
  remote "uloop execute-dynamic-code --code-file $SN\\$1 --project-path $PROJECT" 2>&1 | python3 -c '
import json,sys
t=sys.stdin.read(); s=t.find("{")
d=json.loads(t[s:]) if s>=0 else {}
print(d.get("Result","") if d.get("Success") else "FAIL "+t[:800])'
}
# 作業ワールドの差し替えを解除して PlayMode を止める（正常終了・途中失敗の両方で使う）
# Clear the work-world override and stop PlayMode (used on both success and failure)
teardown() {
  run cleanup.cs >/dev/null || true
  remote "uloop control-play-mode --action Stop --project-path $PROJECT" >/dev/null 2>&1 || true
}
fail() { echo "ERROR: $1（$OUT）" >&2; echo "ERROR: $1" >> "$OUT"; teardown; exit 3; }
# set -e で途中のコマンドが失敗した時も後片付けしてから止める
# Also tear down when a command fails under set -e
trap 'echo "ERROR: 途中で失敗しました（$OUT）" >&2; teardown' ERR

remote "New-Item -ItemType Directory -Force -Path $SN | Out-Null"
scp -q "${SSH_OPTS[@]}" "$SCRIPT_DIR"/snippets/*.cs "$SCRIPT_DIR"/../2026-10-22-gameobject-count/go-breakdown.cs "$HOST:C:/moorestech-perf/snippets/"
echo "commit=$(remote "git -C G:\\GitHub\\moorestech rev-parse HEAD" | tr -d '\r')" > "$OUT"
remote "uloop control-play-mode --action Stop --project-path $PROJECT" >/dev/null 2>&1 || true
remote "\$b='C:\\moorestech-perf\\baselines\\2026-10-22-world_1'; \$w='C:\\moorestech-perf\\work\\current'; if (Test-Path \$w) { Remove-Item \$w -Recurse -Force }; New-Item -ItemType Directory -Force -Path \$w | Out-Null; Copy-Item \"\$b\\*\" \$w -Force"
remote "uloop control-play-mode --action Play --project-path $PROJECT" >/dev/null
for _ in $(seq 1 40); do r="$(run enter-world.cs)"; case "$r" in started*) break ;; esac; sleep 3; done
case "$r" in started*) ;; *) fail "ローカル開始できませんでした: $r" ;; esac
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
[ "$stable" = 1 ] || fail "定常状態になりませんでした"
sleep 20
echo "== go-breakdown" >> "$OUT"; run go-breakdown.cs >> "$OUT"
# Get-Content は SSH 越しに日本語のブロック名が化けるので、UTF-8 のファイルのまま取り込む。取れなければ結果ファイルにも残す
# Get-Content mangles Japanese block names over SSH, so copy the UTF-8 file as is; record a failure in the result file too
if scp -q "${SSH_OPTS[@]}" "$HOST:C:/moorestech-perf/go-breakdown.txt" "$OUT.go-breakdown"; then
  cat "$OUT.go-breakdown" >> "$OUT"; rm -f "$OUT.go-breakdown"
else
  echo "ERROR: go-breakdown.txt を取得できませんでした" | tee -a "$OUT" >&2
fi
# 設置プレビュー枠と当たり判定の数（ブロック配下かどうか・描画中かどうか）
# Count preview frames and hit faces (under blocks or not, rendered or not)
echo "== placement-frame-count steady" >> "$OUT"; run placement-frame-count.cs >> "$OUT"
# 遷移の計測。実行中にスニペットを呼ぶと Editor が止まって計測を乱すので、終わるまで待ってから集計する
# Transition probe; calling snippets while it runs stalls the Editor, so wait until it finishes before collecting
echo "== buildmode-probe" >> "$OUT"
r="$(run buildmode-probe-start.cs)"; echo "$r" >> "$OUT"
case "$r" in "probe installed"*) ;; *) fail "計測を開始できませんでした: $r" ;; esac
sleep 60
for _ in $(seq 1 24); do r="$(run buildmode-probe-collect.cs)"; case "$r" in not-done*) sleep 10 ;; *) break ;; esac; done
echo "$r" >> "$OUT"
[ "$(printf '%s\n' "$r" | head -1)" = "error=" ] || fail "計測が終わらないか失敗しました: $(printf '%s\n' "$r" | head -2)"
# 計測は設置モードに入ったまま終わるので、描画中の枠を数えてから出る
# The probe ends inside build mode, so count the rendered frames before leaving
echo "-- placement-frame-count in-buildmode" >> "$OUT"; run placement-frame-count.cs >> "$OUT"
run buildmode-exit.cs >> "$OUT"; sleep 3
teardown
echo "[buildmode] 保存: $OUT"
