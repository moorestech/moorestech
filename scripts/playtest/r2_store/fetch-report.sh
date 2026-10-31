#!/usr/bin/env bash
# 指定の報告 1 件の生データ（ワールド・スナップショット・動画・bundle 等）を R2 から丸ごと取ってくる。正本は R2 だけ
# 使い方: fetch-report.sh [--kind report|progress] [--out <dir>] <steamId または dev-機材名> <報告ID>
# 成功すると取得先のディレクトリを stdout に 1 行出す。R2 から片付けで消えた箱は理由を出して exit 3、その他の失敗は exit 1
# Downloads one report's full raw data (world, snapshots, video, bundles...) from R2, the only place it lives
# Usage: fetch-report.sh [--kind report|progress] [--out <dir>] <steamId or dev-machine> <report id>
# On success prints the destination directory; a box purged from R2 after settling exits 3 with the reason, other failures exit 1
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
log() { echo "[fetch-report] $*" >&2; }
usage() { log "使い方: fetch-report.sh [--kind report|progress] [--out <dir>] <steamId> <id>"; exit 1; }

REPO="${MOORESTECH_REPO:-$(cd "$HERE/../../.." && pwd)}"
ENV_FILE="${PLAYTEST_ENV_FILE:-$REPO/../../services/playtest/env.sh}"
# shellcheck disable=SC1090
if [ -f "$ENV_FILE" ]; then . "$ENV_FILE"; else log "env file が無い（${ENV_FILE}）。PLAYTEST_ADMIN_KEY は環境変数頼みになる"; fi
# shellcheck source=../lib/receiver-api.sh
. "$HERE/../lib/receiver-api.sh"

kind=report; out=""
while [ "$#" -gt 2 ]; do
  case "$1" in
    --kind) kind="${2:-}"; shift 2 ;;
    --out) out="${2:-}"; shift 2 ;;
    *) usage ;;
  esac
done
[ "$#" -eq 2 ] || usage
steam_id="$1"; id="$2"
case "$kind" in report|progress) ;; *) log "ERROR: --kind は report か progress: ${kind}"; exit 1 ;; esac
# steamId/id は運用者入力。rm -rf・mv のパスへ連結する前に単一の安全セグメントか検証する
# steamId/id are operator input; verify each is a single safe segment before joining it into rm -rf/mv paths
python3 -I "$HERE/../lib/safe_segment.py" segment "$steam_id" && python3 -I "$HERE/../lib/safe_segment.py" segment "$id" \
  || { log "ERROR: steamId/id が安全なパスセグメントでない"; exit 1; }
out="${out:-${TMPDIR:-/tmp}/moorestech-playtest-fetch/$kind/$steam_id/$id}"
partial="$out.partial"
[ ! -e "$out" ] || { log "ERROR: 取得先が既にある（消してからやり直す）: $out"; exit 1; }
rm -rf "$partial" && mkdir -p "$partial" || { log "ERROR: 取得先を作れない: $partial"; exit 1; }
trap 'rm -rf "$partial"' EXIT

# READY が取れなければ箱の在処から確かめる。410 は片付けで生データを消した箱
# Without READY there is nothing to list; a 410 means the box's raw data was purged after settling
if ! receiver_get_object "$kind" "$steam_id" "$id" READY "$partial/READY" 2> "$partial.err"; then
  cat "$partial.err" >&2
  if [ "$RECEIVER_LAST_STATUS" = 410 ]; then
    rm -f "$partial.err"; log "R2 から片付けで削除済みの報告（上の purgedAt を参照）: $kind/$steam_id/$id"; exit 3
  fi
  rm -f "$partial.err"; log "ERROR: READY を取れない（未送信・ID 違い・受け口の障害）: $kind/$steam_id/$id"; exit 1
fi
rm -f "$partial.err"

files_rc=0
files="$(python3 -I "$HERE/../lib/safe_segment.py" ready-files "$partial/READY")" || files_rc=$?
case "$files_rc" in
  0) ;;
  5) log "[WARN] files[] が空（クライアントが全ファイルを見送った箱）。READY だけを置く"; files="" ;;
  *) log "ERROR: READY の files[] を読めない（exit ${files_rc}）: $kind/$steam_id/$id"; exit 1 ;;
esac
count=0
while IFS= read -r rel; do
  [ -n "$rel" ] || continue
  mkdir -p "$partial/$(dirname "$rel")" || { log "ERROR: mkdir 失敗 $partial/$(dirname "$rel")"; exit 1; }
  receiver_get_object "$kind" "$steam_id" "$id" "$rel" "$partial/$rel" || { log "ERROR: 取得失敗 $rel"; exit 1; }
  count=$((count + 1))
done <<< "$files"
# 大きな bundle は __attachments/ の断片で届くので、長さ・SHA256 を照合して元へ戻す
# Large bundles arrive as __attachments/ chunks, so verify their lengths and SHA256 and restore the originals
python3 -I "$HERE/../../bugreport/lib/restore-commit-transfer.py" "$partial" \
  || { log "ERROR: コミット資料（bundle）の復元に失敗"; exit 1; }
mv "$partial" "$out" || { log "ERROR: mv 失敗 $partial -> $out"; exit 1; }
trap - EXIT
log "取得した: $kind/$steam_id/${id}（${count} 件）"
echo "$out"
