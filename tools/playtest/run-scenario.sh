#!/bin/bash
# シナリオ一発実行ランナー: プリフライト→PlayMode起動→ready待ち→シナリオ投入→result.json回収
# One-shot scenario runner: preflight -> boot play mode -> wait ready -> inject scenario -> collect result.json
#
# シナリオファイルは execute-dynamic-code に渡すC#スニペット（PlaytestRunner.Runを呼び、実行ディレクトリを返すこと）
# The scenario file is a C# snippet for execute-dynamic-code (must call PlaytestRunner.Run and return the run dir)
#
# usage: run-scenario.sh <unity-project-path> <scenario.cs> [master-server-dir]
set -u

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_PATH="${1:?usage: run-scenario.sh <unity-project-path> <scenario.cs> [master-server-dir]}"
SCENARIO_FILE="${2:?scenario .cs file required}"
MASTER_DIR="${3:-/Users/katsumi/moorestech-worktrees/playtest-master/server_v8}"
READY_TIMEOUT=300
RESULT_TIMEOUT=420

extract_json() { sed -n '/^{/,$p'; }
json_get() { python3 -c "import sys,json; print(json.load(sys.stdin).get('$1',''))" 2>/dev/null; }

edc() {
    # ドメインリロード直後はEDCが失敗するため、成功するまで最大8回リトライする
    # EDC fails right after a domain reload, so retry up to 8 times until it succeeds
    local attempt response success
    for attempt in $(seq 1 8); do
        response=$(uloop execute-dynamic-code --project-path "$PROJECT_PATH" --code "$1" 2>/dev/null | extract_json)
        success=$(echo "$response" | json_get Success)
        if [[ "$success" == "True" ]]; then
            echo "$response"
            return 0
        fi
        sleep 5
    done
    echo "$response"
    return 1
}

echo "== preflight =="
"$SCRIPT_DIR/preflight.sh" "$PROJECT_PATH" "$MASTER_DIR" || exit 1

echo "== boot play mode =="
IS_PLAYING=$(edc 'return UnityEditor.EditorApplication.isPlaying;' | json_get Result)
if [[ "$IS_PLAYING" == "True" ]]; then
    # 既にPlayMode中ならそのままシナリオを流す（セッションディレクトリは既存 or adhoc）
    # Already playing: inject the scenario as-is (session dir is existing or adhoc)
    echo "already in play mode — skip boot"
else
    BOOT_CODE="using Client.Playtest; return PlaytestBoot.PrepareAndEnterPlayMode(\"$MASTER_DIR\", true);"
    SESSION_DIR=$(edc "$BOOT_CODE" | json_get Result)
    if [[ "$SESSION_DIR" != /* ]]; then
        echo "NG: boot failed: $SESSION_DIR"
        exit 1
    fi
    echo "session: $SESSION_DIR"

    # ドメインリロード＋ゲーム初期化完了(readyマーカー出現)をファイルポーリングで待つ
    # Wait for domain reload + game init (ready marker) by polling the file
    echo "== wait ready (max ${READY_TIMEOUT}s) =="
    WAITED=0
    until [[ -f "$SESSION_DIR/ready.marker" ]]; do
        if [[ $WAITED -ge $READY_TIMEOUT ]]; then
            echo "NG: game not ready within ${READY_TIMEOUT}s"
            exit 1
        fi
        sleep 2; WAITED=$((WAITED + 2))
    done
    echo "ready after ~${WAITED}s"
fi

echo "== inject scenario: $SCENARIO_FILE =="
SCENARIO_RESPONSE=$(edc "$(cat "$SCENARIO_FILE")")
RUN_DIR=$(echo "$SCENARIO_RESPONSE" | json_get Result)
if [[ "$RUN_DIR" != /* ]]; then
    echo "NG: scenario injection failed:"
    echo "$SCENARIO_RESPONSE"
    exit 1
fi
echo "run dir: $RUN_DIR"

echo "== wait result.json (max ${RESULT_TIMEOUT}s) =="
WAITED=0
until [[ -f "$RUN_DIR/result.json" ]]; do
    if [[ $WAITED -ge $RESULT_TIMEOUT ]]; then
        echo "NG: result.json not written within ${RESULT_TIMEOUT}s"
        exit 1
    fi
    sleep 2; WAITED=$((WAITED + 2))
done

echo "== result =="
cat "$RUN_DIR/result.json"

# 成功時はPlayModeを自動停止する（ポート占有・次回実行の持ち越し防止）。
# 失敗時はライブ診断（EDCでの状態確認）のために意図的に残し、停止コマンドを案内する。
# Auto-stop play mode on success (prevents port squatting and state carry-over into the next run).
# On failure, keep play mode alive on purpose for live EDC diagnosis and print how to stop it.
if python3 -c "import sys,json; sys.exit(0 if json.load(open('$RUN_DIR/result.json'))['Success'] else 1)"; then
    echo "== stop play mode =="
    uloop control-play-mode --project-path "$PROJECT_PATH" --action stop >/dev/null 2>&1 && echo "play mode stopped"
    exit 0
else
    echo "NG: scenario failed — PlayModeは診断用に残しています（停止: uloop control-play-mode --project-path $PROJECT_PATH --action stop）"
    exit 1
fi
