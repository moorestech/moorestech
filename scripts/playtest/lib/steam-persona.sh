#!/usr/bin/env bash
# Steam の公開表示名を取得し、失敗も理由付きで記録する（ADR 0070）
# Fetch the public Steam persona and record failures with a reason (ADR 0070)
STEAM_CURL_CMD="${STEAM_CURL_CMD:-curl}"
STEAM_API_MAX_TIME="${STEAM_API_MAX_TIME:-20}"

steam_persona_resolve() {
  local steam_id="$1" out="$2"
  if [ -z "${STEAM_PERSONA_CACHE_DIR:-}" ] || [ ! -d "$STEAM_PERSONA_CACHE_DIR" ]; then
    log "ERROR: STEAM_PERSONA_CACHE_DIR が未設定または存在しない（表示名を保存できない）"
    return 1
  fi
  local cached="$STEAM_PERSONA_CACHE_DIR/$steam_id.json"

  # 同じ実行では SteamID ごとに一度だけ照会する
  # Query each SteamID once per ingestion run
  if [ -f "$cached" ]; then
    cp "$cached" "$out" || { log "ERROR: 表示名キャッシュをコピーできない: $steam_id"; return 1; }
    return 0
  fi
  if [ -z "${STEAM_WEB_API_KEY:-}" ]; then
    log "[WARN] STEAM_WEB_API_KEY 未設定のため表示名を解決しない: $steam_id"
    python3 "$HERE/lib/steam_persona.py" unresolved "STEAM_WEB_API_KEY 未設定" "$cached" \
      || { log "ERROR: 表示名の未解決理由を書けない: $steam_id"; return 1; }
    cp "$cached" "$out" || { log "ERROR: 表示名キャッシュをコピーできない: $steam_id"; return 1; }
    return 0
  fi

  # API 鍵はクエリ文字列に載るため URL をログに出さない
  # The API key rides in the query, so never log the URL
  local body="$STEAM_PERSONA_CACHE_DIR/$steam_id.response" code missing
  code="$("$STEAM_CURL_CMD" --silent --max-time "$STEAM_API_MAX_TIME" -w '%{http_code}' -o "$body" \
    "https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key=${STEAM_WEB_API_KEY}&steamids=${steam_id}")" || code="curl-failed"
  if [ "$code" = 200 ]; then
    python3 "$HERE/lib/steam_persona.py" extract "$body" "$steam_id" "$cached" \
      || { log "ERROR: 表示名の応答を記録できない: $steam_id"; return 1; }
  else
    python3 "$HERE/lib/steam_persona.py" unresolved "GetPlayerSummaries status=${code}" "$cached" \
      || { log "ERROR: 表示名の未解決理由を書けない: $steam_id"; return 1; }
  fi

  # 未解決の理由は JSON と運用ログの両方に残す
  # Put unresolved reasons in both JSON and the operator log
  missing="$(python3 -c 'import json,sys;print(json.load(open(sys.argv[1]))["steamPersonaMissing"])' "$cached")"
  [ -z "$missing" ] || log "[WARN] 表示名を解決できない: ${steam_id}（${missing}）"
  cp "$cached" "$out" || { log "ERROR: 表示名キャッシュをコピーできない: $steam_id"; return 1; }
  return 0
}
