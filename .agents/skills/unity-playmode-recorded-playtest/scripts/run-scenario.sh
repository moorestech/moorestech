#!/bin/bash
# シナリオ一発実行ランナー: プリフライト→PlayMode起動→ready待ち→シナリオ投入→result.json回収
# One-shot scenario runner: preflight -> boot play mode -> wait ready -> inject scenario -> collect result.json
#
# シナリオファイルは execute-dynamic-code に渡すC#スニペット（PlaytestRunner.Runを呼び、実行ディレクトリを返すこと）
# The scenario file is a C# snippet for execute-dynamic-code (must call PlaytestRunner.Run and return the run dir)
#
# usage: run-scenario.sh <unity-project-path> <scenario.cs> [master-server-dir]
# 終了コード: 0 = シナリオ成功、1 = 失敗（preflight・boot・ready・結果）、2 = コード片がコンパイルできず1行も実行されなかった（シナリオ・boot のコード片・preflight のドライランのどれでも）
# Exit codes: 0 = scenario passed, 1 = failure (preflight, boot, ready, result), 2 = a code snippet did not compile and ran zero lines (the scenario, the boot snippet, or the preflight dry run)
set -u

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_PATH="${1:?usage: run-scenario.sh <unity-project-path> <scenario.cs> [master-server-dir]}"
SCENARIO_FILE="${2:?scenario .cs file required}"
source "$SCRIPT_DIR/fixed-world-environment.sh"
source "$SCRIPT_DIR/edc-client.sh"

# 固定worldの入力契約をmaster解決やpreflightより前に確定する
# Resolve the fixed-world input contract before master resolution or preflight
validate_fixed_world_environment
FIXED_WORLD_ENVIRONMENT_STATUS=$?
if [[ "$FIXED_WORLD_ENVIRONMENT_STATUS" -eq 2 ]]; then
    exit 1
fi
if [[ "$FIXED_WORLD_ENVIRONMENT_STATUS" -eq 0 ]]; then
    FIXED_WORLD_BOOT=true
else
    FIXED_WORLD_BOOT=false
fi

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
READY_TIMEOUT="${PLAYTEST_READY_TIMEOUT:-300}"
RESULT_TIMEOUT=420
# boot とシナリオ投入の再試行回数（秒数は EDC_NO_TIMEOUT_SECONDS で外から切らない）
# Retry count for boot and scenario injection (no outside time limit, see EDC_NO_TIMEOUT_SECONDS)
EDC_LONG_CALL_ATTEMPTS=8

csharp_quote() { python3 -c 'import json,sys; print(json.dumps(sys.argv[1]))' "$1"; }

echo "== preflight =="
"$SCRIPT_DIR/preflight.sh" "$PROJECT_PATH" "$MASTER_DIR"
PREFLIGHT_STATUS=$?
# preflight のコンパイル失敗（2）は区別したまま返し、他の失敗は 1 にする
# Pass preflight's compile failure (2) through unchanged; any other failure becomes 1
if [[ "$PREFLIGHT_STATUS" -eq 2 ]]; then exit 2; fi
if [[ "$PREFLIGHT_STATUS" -ne 0 ]]; then exit 1; fi

echo "== boot play mode =="
IS_PLAYING=$(edc_call "$EDC_LONG_CALL_ATTEMPTS" "$EDC_NO_TIMEOUT_SECONDS" 'return UnityEditor.EditorApplication.isPlaying;' | json_get Result)
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
# 検証済みの分類だけで固定worldと従来起動を切り替える
# Switch between fixed-world and legacy boot using only the validated classification
if [[ "$FIXED_WORLD_BOOT" == true ]]; then
    MASTER_LITERAL=$(csharp_quote "$MASTER_DIR")
    WORLD_LITERAL=$(csharp_quote "$PLAYTEST_WORLD_DIRECTORY")
    MAP_MODE_LITERAL=$(csharp_quote "$PLAYTEST_MAP_MODE")
    BOOT_CODE="using Client.Playtest; return PlaytestBoot.PrepareWorldAndEnterPlayMode($MASTER_LITERAL, $WORLD_LITERAL, $MAP_MODE_LITERAL, $PLAYTEST_SEED);"
else
    MASTER_LITERAL=$(csharp_quote "$MASTER_DIR")
    BOOT_CODE="using Client.Playtest; return PlaytestBoot.PrepareAndEnterPlayMode($MASTER_LITERAL, true);"
fi
SESSION_DIR_BEFORE_BOOT=$(edc_call "$EDC_LONG_CALL_ATTEMPTS" "$EDC_NO_TIMEOUT_SECONDS" 'return Client.Playtest.Core.PlaytestPaths.SessionDirectory;' | json_get Result)
BOOT_RESPONSE=$(edc_call "$EDC_LONG_CALL_ATTEMPTS" "$EDC_NO_TIMEOUT_SECONDS" "$BOOT_CODE")
BOOT_STATUS=$?
# 起動コード片がコンパイルできないときは理由を出して止める（PlayMode に入っていないので停止は不要）
# When the boot snippet does not compile, print the reason and stop (not in PlayMode yet, so no stop needed)
if [[ "$BOOT_STATUS" -eq 2 ]]; then
    echo "NG: boot snippet compile failed — PlaytestBoot の API が変わった可能性"
    printf '%s' "$BOOT_RESPONSE" | print_compile_errors
    exit 2
fi
SESSION_DIR=$(echo "$BOOT_RESPONSE" | json_get Result)
# 1 回目の応答が PlayMode 突入で空になり再送が "already playing" を返したときは、直前に停止を確かめているので 1 回目の起動の置き場を SessionState から取る
# 起動前と同じ置き場なら 1 回目は起動していないので NG のまま止める
# If the first reply was lost to the PlayMode entry and the resend says "already playing", take the first boot's dir from SessionState (we confirmed stopped just before)
# The same dir as before the boot means the first attempt never booted, so stay NG
if [[ "$SESSION_DIR" == "ERROR: already playing" ]]; then
    RECOVERED_DIR=$(edc_call "$EDC_LONG_CALL_ATTEMPTS" "$EDC_NO_TIMEOUT_SECONDS" 'return Client.Playtest.Core.PlaytestPaths.SessionDirectory;' | json_get Result)
    if [[ -n "$RECOVERED_DIR" && "$RECOVERED_DIR" != "$SESSION_DIR_BEFORE_BOOT" ]]; then
        echo "WARN: boot reply was lost on PlayMode entry; using the session dir of that boot: $RECOVERED_DIR"
        SESSION_DIR="$RECOVERED_DIR"
    fi
fi
# Windows(git bash)ではUnityが "C:\..." 形式の絶対パスを返すので、ドライブ文字始まりも絶対パスとして扱う
# On Windows (git bash) Unity returns "C:\..."-style absolute paths, so a drive-letter prefix also counts as absolute
is_absolute_path() { case "$1" in /*|[A-Za-z]:*) return 0;; *) return 1;; esac; }
if ! is_absolute_path "$SESSION_DIR"; then
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
SCENARIO_RESPONSE=$(edc_call "$EDC_LONG_CALL_ATTEMPTS" "$EDC_NO_TIMEOUT_SECONDS" "$(cat "$SCENARIO_FILE")")
SCENARIO_STATUS=$?
# コンパイルできないシナリオは1行も実行されていない。検証0件を緑に見せないよう専用の終了コードで止める
# A scenario that does not compile ran zero lines; stop with a dedicated exit code so zero checks never look green
if [[ "$SCENARIO_STATUS" -eq 2 ]]; then
    echo "NG: scenario compile failed — シナリオは1行も実行されていません: $SCENARIO_FILE"
    printf '%s' "$SCENARIO_RESPONSE" | print_compile_errors
    uloop control-play-mode --project-path "$PROJECT_PATH" --action stop >/dev/null 2>&1 && echo "play mode stopped" || echo "WARN: play mode stop failed（手動停止: uloop control-play-mode --project-path $PROJECT_PATH --action stop）"
    exit 2
fi
RUN_DIR=$(echo "$SCENARIO_RESPONSE" | json_get Result)
if ! is_absolute_path "$RUN_DIR"; then
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
if python3 -c "import sys,json; sys.exit(0 if json.load(open(sys.argv[1], encoding='utf-8'))['Success'] else 1)" "$RUN_DIR/result.json"; then
    echo "== stop play mode =="
    uloop control-play-mode --project-path "$PROJECT_PATH" --action stop >/dev/null 2>&1 && echo "play mode stopped"
    exit 0
else
    echo "NG: scenario failed — PlayModeは診断用に残しています（停止: uloop control-play-mode --project-path $PROJECT_PATH --action stop）"
    exit 1
fi
