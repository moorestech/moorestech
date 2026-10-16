#!/usr/bin/env bash
# 開発機ごとの受け口トークンを管理する
# Administer receiver tokens for individual developer machines
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
log() { echo "[dev-token] $*" >&2; }
REPO="${MOORESTECH_REPO:-$(cd "$HERE/../.." && pwd)}"
ENV_FILE="${PLAYTEST_ENV_FILE:-$REPO/../../services/playtest/env.sh}"
# envを先に読み、接続先の展開へ反映する
# Load the environment before resolving the receiver endpoint
if [ -f "$ENV_FILE" ]; then
  . "$ENV_FILE"
else
  log "env file が無い（${ENV_FILE}）。PLAYTEST_ADMIN_KEY 等は環境変数頼みになる"
fi
. "$HERE/lib/receiver-api.sh"
usage() { log "使い方: dev-token.sh issue <machine> | list | revoke <machine>"; exit 1; }
[ "$#" -ge 1 ] || usage
command="$1"; shift
case "$command" in
  issue|revoke)
    [ "$#" -eq 1 ] || usage
    machine="$1"
    [[ "$machine" =~ ^[a-z0-9][a-z0-9-]{0,31}$ ]] \
      || { log "機材名は小文字英数字で始まる1〜32文字の小文字英数字・ハイフンにしてください"; exit 1; } ;;
  list) [ "$#" -eq 0 ] || usage ;;
  *) usage ;;
esac
WORK="$(mktemp -d)" || exit 1
trap 'rm -rf "$WORK"' EXIT
# 生トークンを含む応答は所有者だけが読める一時領域に置く
# Keep responses containing raw tokens in an owner-only temporary directory
case "$command" in
  issue)
    receiver_dev_token_issue "$machine" "$WORK/response.json" || exit 1
    python3 -c 'import json,re,sys
value=json.load(open(sys.argv[1]))["token"]
if not isinstance(value,str) or not re.fullmatch("mdt_[a-f0-9]{64}",value):
    sys.exit("受け口のトークン応答が不正です")
print(value)' "$WORK/response.json" || exit 1
    log "Unity の moorestech/Playtest/Developer Token へ貼り付けて保存してください" ;;
  list)
    receiver_dev_token_list "$WORK/response.json" || exit 1
    python3 -c 'import json,sys
for row in json.load(open(sys.argv[1]))["machines"]:
    print(row["machine"] + "\t" + row["createdAt"])' "$WORK/response.json" || exit 1 ;;
  revoke) receiver_dev_token_revoke "$machine" "$WORK/response.json" || exit 1 ;;
esac
