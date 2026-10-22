#!/usr/bin/env bash
# windows-desktop の Unity Editor でベースラインワールドを読み込み、定常状態のパフォーマンスを計測して JSON に残す
# usage: run-benchmark.sh <label> [baseline-name]
#   label         結果ファイル名に入れる短い名前（例: baseline, dedupe-envelope）
#   baseline-name C:\moorestech-perf\baselines\ 以下のワールド名（既定: 2026-10-22-world_1）
# ワールドの読み込みが一番時間を食うので、1回読み込んで REPEAT 回（既定3回）計測し、回ごとに JSON を出す
# 前提: windows-desktop で Editor が起動済み・計測したいブランチを checkout してコンパイル済み
set -euo pipefail

LABEL="${1:?usage: run-benchmark.sh <label> [baseline-name]}"
BASELINE="${2:-2026-10-22-world_1}"
HOST="${BENCH_HOST:-windows-desktop}"
PROJECT='G:\GitHub\moorestech\moorestech_client'
REPO='G:\GitHub\moorestech'
REMOTE_SNIPPETS='C:\moorestech-perf\snippets'
FPS_WINDOW_SECONDS="${FPS_WINDOW_SECONDS:-10}"
PROFILER_WINDOW_SECONDS="${PROFILER_WINDOW_SECONDS:-6}"
EVENT_WINDOW_SECONDS="${EVENT_WINDOW_SECONDS:-6}"
SETTLE_SECONDS="${SETTLE_SECONDS:-20}"
POLL_SECONDS="${POLL_SECONDS:-5}"
REPEAT="${REPEAT:-3}"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
WORK="$(mktemp -d)"
trap 'ssh "${SSH_OPTS[@]}" -O exit "$HOST" >/dev/null 2>&1; rm -rf "$WORK"' EXIT

# 通信が途切れても固まらないよう、10秒おきの応答確認が3回続けて失敗したら切断する
# 1本の接続を使い回して、コマンドごとの SSH 接続確立を省く
SSH_OPTS=(-o ConnectTimeout=10 -o ServerAliveInterval=10 -o ServerAliveCountMax=3 -o ControlMaster=auto -o "ControlPath=/tmp/bench-ssh-%C" -o ControlPersist=120)
remote() { ssh "${SSH_OPTS[@]}" "$HOST" "$@"; }

# スニペットを1本実行し、uloop応答の Result 文字列だけを標準出力へ出す。失敗なら理由を出して止める
run_snippet() {
    local name="$1" out
    out="$(remote "uloop execute-dynamic-code --code-file $REMOTE_SNIPPETS\\$name --project-path $PROJECT" 2>&1)"
    python3 -c '
import json, sys
text = sys.stdin.read()
start = text.find("{")
if start < 0:
    sys.exit("uloop の応答を読めません: " + text[:500])
data = json.loads(text[start:])
if not data.get("Success"):
    sys.exit("スニペット失敗 " + sys.argv[1] + ": " + json.dumps({k: data.get(k) for k in ("ErrorMessage", "Error", "CompilationErrors")}, ensure_ascii=False)[:1500])
print(data.get("Result", ""))
' "$name" <<<"$out"
}

wait_seconds() { sleep "$1"; }

echo "[bench] スニペットを転送"
remote "New-Item -ItemType Directory -Force -Path $REMOTE_SNIPPETS | Out-Null"
scp -q "${SSH_OPTS[@]}" "$SCRIPT_DIR"/snippets/*.cs "$HOST:C:/moorestech-perf/snippets/"

echo "[bench] 計測対象のコミットを記録"
COMMIT="$(remote "git -C $REPO rev-parse HEAD" | tr -d '\r')"
BRANCH="$(remote "git -C $REPO rev-parse --abbrev-ref HEAD" | tr -d '\r')"
DIRTY="$(remote "git -C $REPO status --porcelain --untracked-files=no" | tr -d '\r' | sed '/^$/d' | wc -l | tr -d ' ')"

echo "[bench] ベースラインワールド $BASELINE を作業用フォルダへ複製"
BASE_HASH="$(remote "\$b='C:\\moorestech-perf\\baselines\\$BASELINE'; if (-not (Test-Path \"\$b\\save.json\")) { Write-Error 'baseline not found'; exit 1 }; \$w='C:\\moorestech-perf\\work\\current'; if (Test-Path \$w) { Remove-Item \$w -Recurse -Force }; New-Item -ItemType Directory -Force -Path \$w | Out-Null; Copy-Item \"\$b\\*\" \$w -Force; (Get-FileHash \"\$b\\save.json\" -Algorithm SHA256).Hash" | tr -d '\r')"

echo "[bench] PlayMode 開始"
remote "uloop control-play-mode --action Stop --project-path $PROJECT" >/dev/null 2>&1 || true
remote "uloop control-play-mode --action Play --project-path $PROJECT" >/dev/null
# タイトル画面の準備が済むまで数秒おきに開始を試みる
for _ in $(seq 1 30); do
    entered="$(run_snippet enter-world.cs)"
    case "$entered" in started*) echo "  $entered"; break ;; esac
    wait_seconds 3
done
case "$entered" in started*) ;; *) echo "ERROR: ローカル開始できませんでした: $entered" >&2; run_snippet cleanup.cs; exit 3 ;; esac

echo "[bench] 定常状態（Renderer 数の変化が${POLL_SECONDS}秒で0.1%以内）を待つ"
prev=-1
for _ in $(seq 1 120); do
    wait_seconds "$POLL_SECONDS"
    cur="$(run_snippet scene-stats.cs | python3 -c 'import json,sys; print(json.load(sys.stdin)["renderers"])')"
    echo "  renderers=$cur"
    # 地形・マップオブジェクトの可視切替で数十個は揺れ続けるので、0.1%以内の変化なら定常とみなす
    if [ "$cur" -gt 1000 ] && [ "$prev" -gt 0 ] && [ $(( (cur > prev ? cur - prev : prev - cur) * 1000 )) -le "$cur" ]; then stable=1; break; fi
    prev="$cur"
done
[ "${stable:-0}" = 1 ] || { echo "ERROR: 定常状態になりませんでした" >&2; run_snippet cleanup.cs; exit 3; }
wait_seconds "$SETTLE_SECONDS"
STAMP="$(date +%Y-%m-%dT%H%M)"
for i in $(seq 1 "$REPEAT"); do
echo "[bench] 計測 $i/$REPEAT"
run_snippet scene-stats.cs >"$WORK/scene.json"
run_snippet environment.cs >"$WORK/environment.json"

echo "  [bench] fps 計測（Profiler なし、${FPS_WINDOW_SECONDS}秒）"
run_snippet fps-start.cs >/dev/null
wait_seconds "$FPS_WINDOW_SECONDS"
run_snippet fps-collect.cs >"$WORK/fps.json"

echo "  [bench] Profiler 計測（${PROFILER_WINDOW_SECONDS}秒）"
run_snippet profiler-start.cs >/dev/null
wait_seconds "$PROFILER_WINDOW_SECONDS"
run_snippet profiler-collect.cs >"$WORK/profiler.json"

echo "  [bench] 受信イベント計測（${EVENT_WINDOW_SECONDS}秒）"
run_snippet events-start.cs >/dev/null
wait_seconds "$EVENT_WINDOW_SECONDS"
run_snippet events-collect.cs >"$WORK/events.json"

OUT="$SCRIPT_DIR/results/${STAMP}_${LABEL}_${COMMIT:0:10}_r${i}.json"
python3 - "$WORK" "$OUT" <<EOF
import json, sys, os
work, out = sys.argv[1], sys.argv[2]
load = lambda n: json.load(open(os.path.join(work, n + ".json")))
result = {
    "schemaVersion": 1,
    "label": "$LABEL",
    "measuredAt": "$STAMP",
    "repeat": {"index": $i, "of": $REPEAT, "sameWorldLoad": True},
    "host": "$HOST",
    "commit": "$COMMIT",
    "branch": "$BRANCH",
    "trackedChangesOnHost": int("$DIRTY"),
    "baselineWorld": {"name": "$BASELINE", "saveJsonSha256": "$BASE_HASH"},
    "windows": {"fpsSeconds": $FPS_WINDOW_SECONDS, "profilerSeconds": $PROFILER_WINDOW_SECONDS, "eventSeconds": $EVENT_WINDOW_SECONDS},
    "environment": load("environment"),
    "scene": load("scene"),
    "fps": load("fps"),
    "profiler": load("profiler"),
    "events": load("events"),
}
json.dump(result, open(out, "w"), ensure_ascii=False, indent=2)
print("[bench] 保存: " + out)
print("[bench] fps=%.2f  PlayerLoop=%.1fms  events/s=%.0f" % (result["fps"]["fps"], result["profiler"]["markers"].get("PlayerLoop", {}).get("totalMs", float("nan")), result["events"]["eventsPerSecond"]))
EOF
done

echo "[bench] 後片付け"
run_snippet cleanup.cs >/dev/null
remote "uloop control-play-mode --action Stop --project-path $PROJECT" >/dev/null

