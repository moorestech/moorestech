#!/usr/bin/env bash
# ship-outbox.sh を ssh/rsync 差し替えで検証する。ローカルの一時 inbox へ「送信」される
# Verifies ship-outbox.sh with ssh/rsync stubs; boxes are "shipped" into a local temp inbox
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

OUTBOX="$TMP/outbox"; INBOX="$TMP/inbox"; mkdir -p "$OUTBOX/20260911_120000_aaaa1111" "$INBOX"
cat > "$OUTBOX/20260911_120000_aaaa1111/manifest.json" <<JSON
{"repository":{"commit":"0000000000000000000000000000000000000000","dirty":false},"masterData":{"commit":"","dirty":false}}
JSON
echo hello > "$OUTBOX/20260911_120000_aaaa1111/screenshot.png"
touch "$OUTBOX/20260911_120000_aaaa1111/READY"
mkdir -p "$OUTBOX/20260911_110000_notready"   # READY が無い箱は送らない

# rsync は cp -R、ssh は引数のコマンドをローカルで実行するスタブ
# rsync stub copies locally; ssh stub runs the remote command locally
cat > "$TMP/rsync" <<'SH'
#!/usr/bin/env bash
src="${@: -2:1}"; dst="${@: -1}"; dst="${dst#*:}"; mkdir -p "$dst"; cp -R "$src"/. "$dst"/
SH
cat > "$TMP/ssh" <<'SH'
#!/usr/bin/env bash
shift; eval "$@"
SH
chmod +x "$TMP/rsync" "$TMP/ssh"

OUTBOX_DIR="$OUTBOX" MACMINI_SSH="stub@host" MACMINI_INBOX="$INBOX" RSYNC_CMD="$TMP/rsync" SSH_CMD="$TMP/ssh" GIT_CMD=true \
  bash "$HERE/../ship-outbox.sh"

test -f "$INBOX/20260911_120000_aaaa1111/READY" || { echo "NG: READY が inbox に無い"; exit 1; }
test -f "$INBOX/20260911_120000_aaaa1111/screenshot.png" || { echo "NG: ファイルが送られていない"; exit 1; }
test -f "$OUTBOX/20260911_120000_aaaa1111/SHIPPED" || { echo "NG: SHIPPED が付いていない"; exit 1; }
test ! -e "$INBOX/20260911_110000_notready" || { echo "NG: READY 無しの箱が送られた"; exit 1; }

# 2回目は何も送らない（SHIPPED 済み）
# A second run ships nothing (already SHIPPED)
rm -rf "$INBOX/20260911_120000_aaaa1111"
OUTBOX_DIR="$OUTBOX" MACMINI_SSH="stub@host" MACMINI_INBOX="$INBOX" RSYNC_CMD="$TMP/rsync" SSH_CMD="$TMP/ssh" GIT_CMD=true \
  bash "$HERE/../ship-outbox.sh"
test ! -e "$INBOX/20260911_120000_aaaa1111" || { echo "NG: SHIPPED 済みの箱が再送された"; exit 1; }
echo "OK"
