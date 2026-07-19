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

# masterピンworktreeを作業中プロジェクトの互換コミットから自動解決する（固定パスを持たない）
# Resolve the pinned master worktree from the working project's own compat commit (no hardcoded path)
resolve_master_dir() {
    local repo_root commit master_repo wt json
    repo_root="$(cd "$PROJECT_PATH/.." 2>/dev/null && pwd)" || return 1
    # Unityが作業ツリーのピンファイルを実チェックアウト値へ常時書き戻すため、コミット済みの値を正とする
    # Unity keeps rewriting the working-tree pin file to the resolved checkout, so trust the committed value
    json=$(git -C "$repo_root" show HEAD:.moorestech-external-revisions.json 2>/dev/null)
    [[ -n "$json" ]] || json=$(cat "$repo_root/.moorestech-external-revisions.json" 2>/dev/null)
    [[ -n "$json" ]] || return 1
    commit=$(printf '%s' "$json" | python3 -c "import json,sys; d=json.load(sys.stdin); print(next((r['commitHash'] for r in d.get('repositories',[]) if r.get('key')=='moorestech_master'),''))" 2>/dev/null)
    [[ -n "$commit" ]] || return 1
    master_repo="$(cd "$repo_root/../moorestech_master" 2>/dev/null && pwd)" || return 1
    wt=$(git -C "$master_repo" worktree list --porcelain 2>/dev/null | awk -v c="$commit" '/^worktree /{p=substr($0,10)} /^HEAD /{if($2==c){print p; exit}}')
    [[ -n "$wt" && -d "$wt/server_v8" ]] || return 1
    echo "$wt/server_v8"
}

MASTER_DIR="${3:-}"
if [[ -z "$MASTER_DIR" ]]; then
    MASTER_DIR=$(resolve_master_dir) || {
        echo "ERROR: masterピンworktreeを自動解決できません。作業中ブランチの互換コミット(.moorestech-external-revisions.jsonのmoorestech_master.commitHash)にHEADを合わせたmoorestech_master worktreeを用意し、第3引数で明示してください:" >&2
        echo "  git -C ../moorestech_master worktree add <path> <互換コミット>" >&2
        exit 1
    }
fi
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
    # PlayMode中は必ず停止してから正規bootする。汚染セッション（前回クラッシュのNREスパム等）に
    # シナリオを注入するとresult.json未生成の沈黙タイムアウトになる（2026-07-18実測: 420秒沈黙）。
    # live診断でPlayModeを維持したい場合はこのランナーではなくEDC直叩きを使うこと。
    # Always stop a live PlayMode before booting: injecting into a polluted session
    # (NRE spam from a prior crash) hangs silently until the result.json timeout.
    echo "play mode already running — stopping for a fresh boot"
    uloop control-play-mode --project-path "$PROJECT_PATH" --action stop >/dev/null 2>&1
    sleep 3
fi
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
