#!/usr/bin/env bash
# 受け口の inbox を moorestech_logs へ取り込み、成功したものだけ ack する（ADR 0061）
# 自動修正ランへの投入はしない。人が日次ダイジェストを見て enqueue-autofix.sh で投入する（裁定 2026-09-13）
# Ingests the receiver inbox into moorestech_logs and acks only what succeeded (ADR 0061)
# It never enqueues auto-fix runs; a human picks them from the daily digest via enqueue-autofix.sh
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
log() { echo "[ingest] $*" >&2; }
now_utc() { date -u +%Y-%m-%dT%H:%M:%SZ; }

# 既定値はスクリプト自身の位置から導出する。supervisor は HOME を封じ込め用に
# 差し替える（~/hermes-agent/data/home）ため、$HOME 基準の既定値は本番で解決しない
# Defaults derive from the script's own location: supervisor swaps HOME for a
# containment dir, so a $HOME-based default would resolve nowhere in production
REPO="${MOORESTECH_REPO:-$(cd "$HERE/../.." && pwd)}"
ENV_FILE="${PLAYTEST_ENV_FILE:-$REPO/../../services/playtest/env.sh}"
# shellcheck disable=SC1090
if [ -f "$ENV_FILE" ]; then
  . "$ENV_FILE"
else
  log "env file が無い（${ENV_FILE}）。PLAYTEST_ADMIN_KEY 等は環境変数頼みになる"
fi

# lib は env.sh 読込の後に source する。先に source すると PLAYTEST_RECEIVER_BASE 等の
# ${VAR:-既定値} 展開が env.sh の export より先に確定してしまい、上書きが効かない
# lib is sourced after env.sh: sourcing it first would resolve its ${VAR:-default}
# expansions before env.sh's exports land, so the override would never take effect
# shellcheck source=lib/receiver-api.sh
. "$HERE/lib/receiver-api.sh"
# shellcheck source=lib/ingest-lock.sh
. "$HERE/lib/ingest-lock.sh"
# shellcheck source=lib/steam-persona.sh
. "$HERE/lib/steam-persona.sh"

LOGS="${MOORESTECH_LOGS:-$REPO/../moorestech_logs}"
PLAYTEST_DIR="$LOGS/harness/playtest"
GIT_CMD="${GIT_CMD:-git}"
GIT_PUSH="${GIT_PUSH:-1}"
MAX_ITEMS="${PLAYTEST_INGEST_MAX_ITEMS:-50}"
MAX_PAGES="${PLAYTEST_INGEST_MAX_PAGES:-20}"
# ロックは logs repo の .git 内の固定パスに置く。$TMPDIR は supervisor 配下と手動実行で異なり相互排他が効かない。
# .git 内なら harness/playtest の git add にも掴まれない
# The lock lives at a fixed path inside the logs repo's .git: $TMPDIR differs between supervisor and manual runs,
# so it would not exclude them; inside .git it is also never picked up by the harness/playtest git add
LOCK="$LOGS/.git/moorestech-playtest-ingest.lock"

# R2 側の kind（report|progress）と logs 側のディレクトリ名を明示対応させる
# Maps the receiver kind (report|progress) onto the logs directory name explicitly
dest_subdir() {
  case "$1" in
    report) echo "reports" ;;
    progress) echo "progress" ;;
    *) return 1 ;;
  esac
}

ingest_one() {
  local kind="$1" steam_id="$2" id="$3" ready_at="$4"
  local sub; sub="$(dest_subdir "$kind")" || { log "ERROR: 未知の kind=${kind} id=${id}（ack しない）"; return 1; }
  # steamId/id は受け口由来の外部入力。rm -rf・mv のパスへ連結する前に単一の安全セグメントか検証する
  # steamId/id come from the receiver; verify each is a single safe segment before joining it into rm -rf/mv paths
  python3 "$HERE/lib/safe_segment.py" segment "$steam_id" && python3 "$HERE/lib/safe_segment.py" segment "$id" \
    || { log "ERROR: steamId/id が安全なパスセグメントでない: $kind/$steam_id/${id}（ack しない）"; return 1; }
  local dest="$PLAYTEST_DIR/$sub/$steam_id/$id"
  local partial="$PLAYTEST_DIR/$sub/$steam_id/$id.partial"
  local persona_file="$STEAM_PERSONA_CACHE_DIR/$kind-$steam_id-$id.persona.json"

  # 既に置かれている＝前回 ack だけ失敗した箱。落とし直さず ack だけやり直す
  # An existing dest means only the ack failed last time: never re-download, just re-ack
  if [ ! -d "$dest" ]; then
    rm -rf "$partial" || { log "ERROR: 前回残骸の削除に失敗 $partial（ack しない）"; return 1; }
    mkdir -p "$partial" || { log "ERROR: mkdir 失敗 $partial（ack しない）"; return 1; }
    receiver_get_object "$kind" "$steam_id" "$id" READY "$partial/READY" \
      || { log "ERROR: READY 取得失敗 $kind/$steam_id/$id"; rm -rf "$partial"; return 1; }
    local files py_rc=0
    files="$(python3 "$HERE/lib/safe_segment.py" ready-files "$partial/READY")" || py_rc=$?
    if [ "$py_rc" -ne 0 ] && [ "$py_rc" -ne 5 ]; then
      case "$py_rc" in
        3) log "ERROR: READY 本文に files[] が無い: $kind/$steam_id/${id}（files を書く前の古いクライアントの要約。ack しない）" ;;
        4) log "ERROR: READY 本文の files[] に不正なパスを含む: $kind/$steam_id/${id}（ack しない）" ;;
        6) log "ERROR: READY 本文の files[] が取り込み側の予約名（ingest.json・READY・AUTOFIX_* 等）と衝突: $kind/$steam_id/${id}（ack しない）" ;;
        *) log "ERROR: READY 本文の解析に失敗（exit ${py_rc}）: $kind/$steam_id/${id}（ack しない）" ;;
      esac
      rm -rf "$partial"; return 1
    fi
    # files[] が空配列＝クライアントが全ファイルを見送った正規の箱。READY と ingest.json だけで公開し ack する
    # An empty files[] is a legitimate box whose client skipped every file; publish READY and ingest.json alone and ack
    if [ "$py_rc" -eq 5 ]; then
      log "[WARN] files[] が空（全ファイル見送り・skipped ${files} 件）。READY と ingest.json だけで公開する: $kind/$steam_id/${id}"
      files=""
    fi
    local rel
    while IFS= read -r rel; do
      [ -n "$rel" ] || continue
      mkdir -p "$partial/$(dirname "$rel")" \
        || { log "ERROR: mkdir 失敗 $partial/$(dirname "$rel")（ack しない）"; rm -rf "$partial"; return 1; }
      receiver_get_object "$kind" "$steam_id" "$id" "$rel" "$partial/$rel" \
        || { log "ERROR: 取得失敗 $id/$rel"; rm -rf "$partial"; return 1; }
    done <<< "$files"
    steam_persona_resolve "$steam_id" "$persona_file" \
      || { log "ERROR: 表示名の結果を書けない $kind/$steam_id/$id（ack しない）"; rm -rf "$partial"; return 1; }
    python3 "$HERE/lib/ingest_metadata.py" \
      "$kind" "$steam_id" "$id" "$ready_at" "$(now_utc)" "$persona_file" "$partial/ingest.json" \
      || { log "ERROR: ingest.json 書き込み失敗 $kind/$steam_id/$id（ack しない）"; rm -rf "$partial"; return 1; }
    mkdir -p "$(dirname "$dest")" || { log "ERROR: mkdir 失敗 $dest（ack しない）"; rm -rf "$partial"; return 1; }
    mv "$partial" "$dest" || { log "ERROR: mv 失敗 $partial -> $dest（ack しない）"; rm -rf "$partial"; return 1; }
    log "ingested: $kind/$steam_id/$id"
  else
    log "取り込み済み。ack のみやり直す: $kind/$steam_id/$id"
  fi

  receiver_ack "$kind" "$steam_id" "$id" || { log "ERROR: ack 失敗 ${id}（次回に持ち越し）"; return 1; }
}

commit_logs() {
  # 記録の commit と push は別々に判定する（scripts/bugreport/inbox-poller.sh 前例）。
  # push だけ失敗を潰すと「届いていない」が誰にも見えなくなる
  # Judge commit and push separately (precedent: inbox-poller.sh); swallowing a failed
  # push alone would hide that nothing reached the remote
  [ -d "$LOGS/.git" ] || { log "ERROR: logs repo が無い: ${LOGS}（commit しない）"; return 0; }
  (
    cd "$LOGS"
    $GIT_CMD add -A harness/playtest || exit 1
    $GIT_CMD diff --cached --quiet -- harness/playtest && exit 0
    # 同じ index を触る他ジョブ（bug-report poller 等）が commit を挟んでも、この commit は
    # harness/playtest だけを含める（pathspec 限定。取り込み記録が他ジョブへ混入しない）
    # Even if another job (e.g. the bug-report poller) shares this index and commits in between,
    # this commit stays scoped to harness/playtest via pathspec so it never mixes into their record
    $GIT_CMD commit -qm "playtest: ingest $(now_utc)" -- harness/playtest || exit 1
  ) || log "ERROR: logs repo の commit に失敗（次回に持ち越し）"
  [ "$GIT_PUSH" = 1 ] || return 0
  (
    cd "$LOGS"
    local ahead
    # 数えられない（上流未設定等）を 0 と読み替えると push 漏れが無音になるので理由を出す
    # Reading a failed count (e.g. no upstream) as 0 would silently skip the push, so log why
    ahead="$($GIT_CMD rev-list --count '@{u}..HEAD' 2>&1)" \
      || { log "ERROR: upstream との差を数えられず push しない: ${ahead}"; exit 0; }
    [ "$ahead" != 0 ] || exit 0
    $GIT_CMD push -q || exit 1
  ) || log "ERROR: logs repo の push に失敗（次回に持ち越し）"
}

# logs repo が無ければ受け口に触る前に止める。位置から導出した既定パスは
# 本体clone以外（タスクworktree等）で解決しないため、ここで fail-closed にする
# Stop before touching the receiver if the logs repo is absent: a location-derived
# default resolves to nothing outside the main clone (task worktrees etc.), so fail closed here
[ -d "${LOGS}/.git" ] || { log "ERROR: logs repo が無い（${LOGS}）。取り込み・ack をしない"; exit 1; }

acquire_lock || exit 0
WORK="$(mktemp -d)"
trap 'rm -rf "$LOCK" "$WORK"' EXIT
STEAM_PERSONA_CACHE_DIR="$WORK/steam-persona"
mkdir -p "$STEAM_PERSONA_CACHE_DIR" || { log "ERROR: 表示名キャッシュを作れない: $STEAM_PERSONA_CACHE_DIR"; exit 1; }

ITEMS="$WORK/items.txt"; : > "$ITEMS"
cursor=""; page=0
while [ "$page" -lt "$MAX_PAGES" ]; do
  page=$((page + 1))
  receiver_inbox_page "$cursor" "$WORK/page.json" \
    || { log "ERROR: /v1/inbox の取得に失敗（page=${page}）。今回は何もしない"; exit 0; }
  python3 -c '
import json,sys
d=json.load(open(sys.argv[1]))
for it in d.get("items") or []:
    row=[str(it.get(k) or "") for k in ("kind","steamId","id","readyAt")]
    if any(c < " " or c == "\x7f" for c in "".join(row)):
        print("[ingest] ERROR: 制御文字を含む item を飛ばす（行区切りを壊すため）: %r" % (row,), file=sys.stderr); continue
    print("\x1f".join(row))
' "$WORK/page.json" >> "$ITEMS"
  cursor="$(python3 -c 'import json,sys;print(json.load(open(sys.argv[1])).get("cursor") or "")' "$WORK/page.json")"
  [ -n "$cursor" ] || break
done

# MAX_ITEMS は成功件数で数える。試行件数で数えると、恒久失敗する箱（files[]欠落等）が
# 上限を占有し続け、後続の正常な箱に永久に到達できなくなる
# MAX_ITEMS counts successes, not attempts: counting attempts would let permanently-failing
# boxes (e.g. missing files[]) occupy the cap forever and starve later, healthy boxes
success=0; failed=0
# 区切りは US(0x1f)。タブは IFS 空白扱いで連続が畳まれ、空フィールドがあると列がずれる
# Fields are split on US (0x1f): tabs are IFS whitespace, so consecutive ones collapse and shift columns when a field is empty
while IFS=$'\x1f' read -r kind steam_id id ready_at; do
  # 空行だけ飛ばす。欠けたフィールドは ingest_one の検証で理由付きで拒否される
  # Skip only blank rows; missing fields are rejected with a reason by ingest_one's validation
  [ -n "${kind}${steam_id}${id}" ] || continue
  if [ "$success" -ge "$MAX_ITEMS" ]; then log "成功 $MAX_ITEMS 件に達した。残りは次回"; break; fi
  if ingest_one "$kind" "$steam_id" "$id" "$ready_at"; then
    success=$((success + 1))
  else
    failed=$((failed + 1))
  fi
done < "$ITEMS"

commit_logs
log "done: 取得 $(wc -l < "$ITEMS" | tr -d ' ') 件を走査 / 成功 ${success} / 失敗 ${failed}"
