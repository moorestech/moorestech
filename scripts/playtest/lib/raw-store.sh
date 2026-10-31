#!/usr/bin/env bash
# 取り込んだ箱を生データ置き場と logs repo へ分けて公開する。呼び出し側が HERE・REPO・log を定義してから source する
# Publishes an ingested box split between the raw store and the logs repo; the caller defines HERE, REPO and log before sourcing
# 生データ（スナップショット・ワールド・動画等）は git の永久履歴へ入れず、期限つきで logs repo の外へ置く（raw_store/layout.py）
# Raw data (snapshots, worlds, videos...) never enters permanent git history; it lives outside the logs repo with an expiry

RAW="${MOORESTECH_PLAYTEST_RAW:-$REPO/../moorestech_playtest_raw}"
RAW_SPLIT_PY="$HERE/raw_store/split.py"

# 生データ置き場が無ければ取り込みを始めない。既定パスは本体 clone 以外で解決せず、黙って作ると見当違いの場所へ溜まるため作らない
# Do not start without the raw store: the default resolves only beside the main clone, so creating it silently could pile data in the wrong place
require_raw_store() {
  [ -d "$RAW" ] && return 0
  log "ERROR: 生データ置き場が無い（${RAW}）。mkdir するか MOORESTECH_PLAYTEST_RAW で指す。取り込まない"
  return 1
}

# 下書き箱から生データを抜いて生データ側を先に公開し、最後に logs 側の箱を mv で公開する。失敗は理由を出して 1
# 生データ側だけ公開されて logs 側が公開されなかった場合は、次回の取り込みが同じ箱を落とし直して置き換える
# Moves raw data out of the draft, publishes the raw side first and the logs box last via mv; failures log a reason and return 1.
# If only the raw side got published, the next ingest re-downloads the same box and replaces it
raw_publish_box() {
  local sub="$1" steam_id="$2" id="$3" partial="$4" dest="$5"
  local rel="$sub/$steam_id/$id"
  local raw_dest="$RAW/$rel" raw_partial="$RAW/$rel.partial" moved
  rm -rf "$raw_partial" || { log "ERROR: 生データ側の前回残骸を消せない: $raw_partial"; return 1; }
  moved="$(python3 -I "$RAW_SPLIT_PY" "$partial" "$raw_partial" "$rel" "$RAW")" \
    || { log "ERROR: 生データを分けられない: $rel"; rm -rf "$raw_partial"; return 1; }
  if [ "$moved" != 0 ]; then
    if [ -e "$raw_dest" ]; then
      log "[WARN] 前回 logs 側の公開まで届かなかった生データを置き換える: $raw_dest"
      rm -rf "$raw_dest" || { log "ERROR: 古い生データを消せない: $raw_dest"; rm -rf "$raw_partial"; return 1; }
    fi
    { mkdir -p "$(dirname "$raw_dest")" && mv "$raw_partial" "$raw_dest"; } \
      || { log "ERROR: 生データを公開できない: $raw_partial -> $raw_dest"; rm -rf "$raw_partial"; return 1; }
    log "生データ ${moved} 件を logs repo の外へ置いた: $raw_dest"
  fi
  mv "$partial" "$dest" || { log "ERROR: mv 失敗 $partial -> $dest"; return 1; }
}
