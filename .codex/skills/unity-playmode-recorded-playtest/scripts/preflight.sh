#!/bin/bash
# プレイテスト実行前のプリフライトチェックを1コマンドで行う
# One-shot preflight checks before running a playtest
#
# 検査項目: (1)CLI Loop疎通(タイムアウト=モーダル/ビジー検出を兼ねる) (2)コンパイル (3)masterデータ実在 (4)マスタロードのドライラン(スキーマ不整合検出) (5)ゲームサーバーポート空き
# Checks: (1) CLI loop ping (timeout doubles as modal/busy detection) (2) compile (3) master data presence (4) master-load dry run (schema mismatch detection) (5) game server port availability
#
# usage: preflight.sh <unity-project-path> [master-server-dir]
set -u

PROJECT_PATH="${1:?usage: preflight.sh <unity-project-path> [master-server-dir]}"

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

MASTER_DIR="${2:-}"
if [[ -z "$MASTER_DIR" ]]; then
    MASTER_DIR=$(resolve_master_dir) || {
        echo "ERROR: masterピンworktreeを自動解決できません。作業中ブランチの互換コミット(.moorestech-external-revisions.jsonのmoorestech_master.commitHash)にHEADを合わせたmoorestech_master worktreeを用意し、第2引数で明示してください:" >&2
        echo "  git -C ../moorestech_master worktree add <path> <互換コミット>" >&2
        exit 1
    }
fi
FAIL=0

# uloop出力から先頭の警告行を除いてJSON部分だけを取り出す
# Strip leading warning lines from uloop output and keep only the JSON part
extract_json() { sed -n '/^{/,$p'; }
json_get() { python3 -c "import sys,json; print(json.load(sys.stdin).get('$1',''))" 2>/dev/null; }

# GNU timeoutが無い環境（素のmacOS）では秒数指定を無視して直接実行するフォールバックを使う
# Fall back to direct execution on systems without GNU timeout (stock macOS); uloop has its own internal timeouts
if ! command -v timeout >/dev/null 2>&1; then
    timeout() { shift; "$@"; }
fi

# ドメインリロード直後のEDC失敗に備えて成功までリトライする
# Retry EDC until it succeeds, covering failures right after a domain reload
edc_retry() {
    local attempt response
    for attempt in $(seq 1 6); do
        response=$(timeout 30 uloop execute-dynamic-code --project-path "$PROJECT_PATH" --code "$1" 2>/dev/null | extract_json)
        if [[ "$(echo "$response" | json_get Success)" == "True" ]]; then
            echo "$response"
            return 0
        fi
        sleep 5
    done
    echo "$response"
    return 1
}

echo "== [1/5] CLI Loop疎通（モーダル/ビジー検出） =="
# ドメインリロード・インポート中を跨げるようリトライ付きで疎通確認する
# Ping with retries so a domain reload or import in progress doesn't fail the check
PING=$(edc_retry 'return "pong:" + UnityEditor.EditorApplication.isPlaying;' | json_get Result)
if [[ "$PING" == pong:* ]]; then
    echo "OK: editor responding (isPlaying=${PING#pong:})"
else
    echo "NG: editor not responding — モーダルダイアログ/ビジー/未起動の可能性"
    FAIL=1
fi

echo "== [2/5] コンパイル =="
COMPILE=""
for _ in 1 2 3; do
    COMPILE=$(uloop compile --project-path "$PROJECT_PATH" 2>/dev/null | extract_json)
    [[ "$(echo "$COMPILE" | json_get Success)" == "True" || -n "$(echo "$COMPILE" | json_get ErrorCount)" ]] && break
    sleep 5
done
if [[ "$(echo "$COMPILE" | json_get Success)" == "True" ]]; then
    echo "OK: compile clean"
else
    echo "NG: compile errors:"
    echo "$COMPILE" | python3 -c "import sys,json; [print('  '+e['Message']) for e in json.load(sys.stdin).get('Errors',[])]" 2>/dev/null
    FAIL=1
fi

echo "== [3/5] masterデータ =="
if [[ -d "$MASTER_DIR/mods" && -n "$(ls -A "$MASTER_DIR/mods" 2>/dev/null)" && -f "$MASTER_DIR/map/map.json" ]]; then
    echo "OK: $MASTER_DIR (mods + map/map.json)"
else
    echo "NG: master data incomplete at $MASTER_DIR (mods/ or map/map.json missing)"
    FAIL=1
fi

echo "== [4/5] マスタロードのドライラン（スキーマ不整合検出） =="
# PlayMode前にEditモードでMasterHolder.Loadを試し、MooresmasterLoaderException等を事前に炙り出す
# Try MasterHolder.Load in edit mode before play to surface MooresmasterLoaderException-class failures early
DRYRUN_CODE="var modsDir = System.IO.Path.Combine(@\"$MASTER_DIR\", \"mods\");
var modResource = new Mod.Loader.ModsResource(modsDir);
var container = new Core.Master.MasterJsonFileContainer(Mod.Config.ModJsonStringLoader.GetMasterString(modResource));
Core.Master.MasterHolder.Load(container);
return \"master-load-ok\";"
DRYRUN=$(edc_retry "$DRYRUN_CODE")
if [[ "$(echo "$DRYRUN" | json_get Result)" == "master-load-ok" ]]; then
    echo "OK: master data loads against current schema"
else
    echo "NG: master load failed (schema/data mismatch?):"
    echo "$DRYRUN" | json_get ErrorMessage | head -5
    echo "$DRYRUN" | json_get Error | head -5
    FAIL=1
fi

echo "== [5/5] ゲームサーバーポート(11564)の空き =="
# サーバーポートは固定値のため、他worktreeのPlayModeが掴んでいると起動が「Address already in use」で無言死する。
# PlayMode停止後もソケットがリークして残ることがあり、その場合は当該EditorへRequestScriptReload()でドメインリロードを要求すると解放される。
# The game server port is a fixed constant; if another worktree's play mode holds it, boot dies silently with
# "Address already in use". Sockets can also leak after play mode stops; a RequestScriptReload() on that editor frees them.
if [[ "$PING" == "pong:True" ]]; then
    echo "OK: 自プロジェクトがPlayMode中（ポートは自サーバーが保持している想定）"
else
    # LISTENのみを塞がり扱いにする。停止済みPlayModeのESTABLISHEDペア残留はbindを妨げない（実測: bind可）
    # Treat only LISTEN as blocking; stale ESTABLISHED pairs left by a stopped play mode do not prevent bind (verified)
    PORT_HOLDERS=$(lsof -nP -iTCP:11564 -sTCP:LISTEN 2>/dev/null | grep "LISTEN" || true)
    if [[ -z "$PORT_HOLDERS" ]]; then
        echo "OK: port 11564 free"
        STALE_PAIRS=$(lsof -nP -iTCP:11564 2>/dev/null | grep "ESTABLISHED" || true)
        [[ -n "$STALE_PAIRS" ]] && echo "  note: 残留ESTABLISHEDペアあり（無害）: $(echo "$STALE_PAIRS" | head -1)"
    else
        echo "NG: port 11564 in use（他worktreeのPlayMode/ソケットリークの可能性）:"
        echo "$PORT_HOLDERS" | head -4
        echo "  対処: 当該Editorのplay mode停止 → 残る場合は execute-dynamic-code で UnityEditor.EditorUtility.RequestScriptReload()"
        FAIL=1
    fi
fi

if [[ $FAIL -ne 0 ]]; then
    echo "PREFLIGHT: FAIL"
    exit 1
fi
echo "PREFLIGHT: PASS"
