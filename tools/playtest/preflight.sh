#!/bin/bash
# プレイテスト実行前のプリフライトチェックを1コマンドで行う
# One-shot preflight checks before running a playtest
#
# 検査項目: (1)CLI Loop疎通(タイムアウト=モーダル/ビジー検出を兼ねる) (2)コンパイル (3)masterデータ実在 (4)マスタロードのドライラン(スキーマ不整合検出)
# Checks: (1) CLI loop ping (timeout doubles as modal/busy detection) (2) compile (3) master data presence (4) master-load dry run (schema mismatch detection)
#
# usage: preflight.sh <unity-project-path> [master-server-dir]
set -u

PROJECT_PATH="${1:?usage: preflight.sh <unity-project-path> [master-server-dir]}"
MASTER_DIR="${2:-/Users/katsumi/moorestech-worktrees/playtest-master/server_v8}"
FAIL=0

# uloop出力から先頭の警告行を除いてJSON部分だけを取り出す
# Strip leading warning lines from uloop output and keep only the JSON part
extract_json() { sed -n '/^{/,$p'; }
json_get() { python3 -c "import sys,json; print(json.load(sys.stdin).get('$1',''))" 2>/dev/null; }

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

echo "== [1/4] CLI Loop疎通（モーダル/ビジー検出） =="
# ドメインリロード・インポート中を跨げるようリトライ付きで疎通確認する
# Ping with retries so a domain reload or import in progress doesn't fail the check
PING=$(edc_retry 'return "pong:" + UnityEditor.EditorApplication.isPlaying;' | json_get Result)
if [[ "$PING" == pong:* ]]; then
    echo "OK: editor responding (isPlaying=${PING#pong:})"
else
    echo "NG: editor not responding — モーダルダイアログ/ビジー/未起動の可能性"
    FAIL=1
fi

echo "== [2/4] コンパイル =="
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

echo "== [3/4] masterデータ =="
if [[ -d "$MASTER_DIR/mods" && -n "$(ls -A "$MASTER_DIR/mods" 2>/dev/null)" && -f "$MASTER_DIR/map/map.json" ]]; then
    echo "OK: $MASTER_DIR (mods + map/map.json)"
else
    echo "NG: master data incomplete at $MASTER_DIR (mods/ or map/map.json missing)"
    FAIL=1
fi

echo "== [4/4] マスタロードのドライラン（スキーマ不整合検出） =="
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

if [[ $FAIL -ne 0 ]]; then
    echo "PREFLIGHT: FAIL"
    exit 1
fi
echo "PREFLIGHT: PASS"
