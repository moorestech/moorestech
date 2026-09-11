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

# 公開先が既にある場合は入れ子破損を避けて送らず SHIPPED も付けない
# When the destination already exists, ship nothing and leave SHIPPED off (no silent nesting)
OUTBOX2="$TMP/outbox2"; mkdir -p "$OUTBOX2/20260911_140000_bbbb2222" "$INBOX/20260911_140000_bbbb2222"
echo '{"repository":{"commit":"","dirty":false},"masterData":{"commit":"","dirty":false}}' > "$OUTBOX2/20260911_140000_bbbb2222/manifest.json"
touch "$OUTBOX2/20260911_140000_bbbb2222/READY"
OUTBOX_DIR="$OUTBOX2" MACMINI_SSH="stub@host" MACMINI_INBOX="$INBOX" RSYNC_CMD="$TMP/rsync" SSH_CMD="$TMP/ssh" GIT_CMD=true \
  bash "$HERE/../ship-outbox.sh"
test ! -e "$OUTBOX2/20260911_140000_bbbb2222/SHIPPED" || { echo "NG: 宛先が既存なのに SHIPPED が付いた"; exit 1; }
test ! -e "$INBOX/20260911_140000_bbbb2222/20260911_140000_bbbb2222.partial" || { echo "NG: 入れ子で公開された"; exit 1; }

# attach_bundle の主経路（一時ref→bundle作成→ref削除）を実 git で踏む
# Exercise attach_bundle's real path (temp ref -> bundle create -> ref delete) with real git
REPO="$TMP/repo"; git init -q --bare "$TMP/origin.git"; git clone -q "$TMP/origin.git" "$REPO"
(
  cd "$REPO" && git config user.email t@t && git config user.name t
  echo a > a.txt && git add a.txt && git commit -qm base && git push -q origin HEAD:master
  echo b > b.txt && git add b.txt && git commit -qm unpushed
)
git -C "$REPO" fetch -q origin
PUSHED_COMMIT="$(git -C "$REPO" rev-parse origin/master)"
UNPUSHED_COMMIT="$(git -C "$REPO" rev-parse HEAD)"
OUTBOX3="$TMP/outbox3"; INBOX3="$TMP/inbox3"; mkdir -p "$OUTBOX3/bundle_case" "$OUTBOX3/pushed_case" "$INBOX3"
printf '{"repository":{"commit":"%s","dirty":false},"masterData":{"commit":"","dirty":false}}\n' "$UNPUSHED_COMMIT" > "$OUTBOX3/bundle_case/manifest.json"
printf '{"repository":{"commit":"%s","dirty":false},"masterData":{"commit":"","dirty":false}}\n' "$PUSHED_COMMIT" > "$OUTBOX3/pushed_case/manifest.json"
touch "$OUTBOX3/bundle_case/READY" "$OUTBOX3/pushed_case/READY"
OUTBOX_DIR="$OUTBOX3" MACMINI_SSH="stub@host" MACMINI_INBOX="$INBOX3" RSYNC_CMD="$TMP/rsync" SSH_CMD="$TMP/ssh" \
  MOORESTECH_REPO="$REPO" MOORESTECH_MASTER="$TMP/no-such-master" \
  bash "$HERE/../ship-outbox.sh"
test -f "$OUTBOX3/bundle_case/repo/commits.bundle" || { echo "NG: bundle が作られていない"; exit 1; }
git -C "$REPO" bundle list-heads "$OUTBOX3/bundle_case/repo/commits.bundle" > "$TMP/bundle-heads.txt"
grep -q "$UNPUSHED_COMMIT" "$TMP/bundle-heads.txt" || { echo "NG: bundle に未pushコミットが入っていない"; exit 1; }
test -f "$INBOX3/bundle_case/repo/commits.bundle" || { echo "NG: bundle が inbox へ運ばれていない"; exit 1; }
if git -C "$REPO" show-ref --verify --quiet refs/bugreport/bundle_case; then echo "NG: 一時 ref が残っている"; exit 1; fi
test ! -e "$OUTBOX3/pushed_case/repo/commits.bundle" || { echo "NG: push 済みコミットで bundle を作った"; exit 1; }
# mv が汎用失敗した場合も理由をログして SHIPPED を付けない
# A generic mv failure also logs a reason and leaves SHIPPED off
cat > "$TMP/ssh-fail" <<'SH'
#!/usr/bin/env bash
exit 1
SH
chmod +x "$TMP/ssh-fail"
OUTBOX4="$TMP/outbox4"; INBOX4="$TMP/inbox4"; mkdir -p "$OUTBOX4/mvfail_case" "$INBOX4"
echo '{"repository":{"commit":"","dirty":false},"masterData":{"commit":"","dirty":false}}' > "$OUTBOX4/mvfail_case/manifest.json"
touch "$OUTBOX4/mvfail_case/READY"
OUTBOX_DIR="$OUTBOX4" MACMINI_SSH="stub@host" MACMINI_INBOX="$INBOX4" RSYNC_CMD="$TMP/rsync" SSH_CMD="$TMP/ssh-fail" GIT_CMD=true \
  bash "$HERE/../ship-outbox.sh" 2>"$TMP/mvfail.log"
grep -q "公開 mv 失敗（exit 1）" "$TMP/mvfail.log" || { echo "NG: mv 失敗の理由がログされていない"; exit 1; }
test ! -e "$OUTBOX4/mvfail_case/SHIPPED" || { echo "NG: mv 失敗なのに SHIPPED が付いた"; exit 1; }

echo "OK"
