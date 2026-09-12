#!/usr/bin/env bash
# ship-outbox.sh を ssh/rsync 差し替えで検証する。ローカルの一時 inbox へ「送信」される
# Verifies ship-outbox.sh with ssh/rsync stubs; boxes are "shipped" into a local temp inbox
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
TMP="$(mktemp -d)"
trap 'rm -rf "${TMP:?}"' EXIT

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
shift; [ "$*" = "true" ] && exit 0   # 到達性検査だけ通し、公開 mv を失敗させる / reachable, but mv fails
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

# C8: 1箱が詰まっても後続の箱は運ばれ、詰まった箱は SHIP_BLOCKED で理由を残して次回から飛ばされる
# C8: one stuck box never blocks the rest; it keeps a SHIP_BLOCKED reason and is skipped afterwards
OUTBOX5="$TMP/outbox5"; INBOX5="$TMP/inbox5"
mkdir -p "$OUTBOX5/20260911_150000_blocked" "$OUTBOX5/20260911_160000_later" "$INBOX5/20260911_150000_blocked"
for id in 20260911_150000_blocked 20260911_160000_later; do
  echo '{"repository":{"commit":"","dirty":false},"masterData":{"commit":"","dirty":false}}' > "$OUTBOX5/$id/manifest.json"
  touch "$OUTBOX5/$id/READY"
done
OUTBOX_DIR="$OUTBOX5" MACMINI_SSH="stub@host" MACMINI_INBOX="$INBOX5" RSYNC_CMD="$TMP/rsync" SSH_CMD="$TMP/ssh" GIT_CMD=true \
  bash "$HERE/../ship-outbox.sh" 2>"$TMP/hol.log"
test -f "$OUTBOX5/20260911_150000_blocked/SHIP_BLOCKED" || { echo "NG: 詰まった箱に SHIP_BLOCKED が無い"; exit 1; }
test ! -e "$OUTBOX5/20260911_150000_blocked/SHIPPED" || { echo "NG: 詰まった箱に SHIPPED が付いた"; exit 1; }
test -f "$OUTBOX5/20260911_160000_later/SHIPPED" || { echo "NG: 先頭が詰まって後続の箱が運ばれていない"; exit 1; }
test -f "$INBOX5/20260911_160000_later/READY" || { echo "NG: 後続の箱が inbox に届いていない"; exit 1; }

# 2回目: 詰まった箱は件数だけ出して飛ばし、新しい箱は運ぶ
# Second run: the stuck box is skipped (count only) while a new box still ships
mkdir -p "$OUTBOX5/20260911_170000_fresh"
echo '{"repository":{"commit":"","dirty":false},"masterData":{"commit":"","dirty":false}}' > "$OUTBOX5/20260911_170000_fresh/manifest.json"
touch "$OUTBOX5/20260911_170000_fresh/READY"
OUTBOX_DIR="$OUTBOX5" MACMINI_SSH="stub@host" MACMINI_INBOX="$INBOX5" RSYNC_CMD="$TMP/rsync" SSH_CMD="$TMP/ssh" GIT_CMD=true \
  bash "$HERE/../ship-outbox.sh" 2>"$TMP/hol2.log"
grep -q "手動確認待ちの箱が 1 件ある" "$TMP/hol2.log" || { echo "NG: 詰まった箱の件数がログされていない"; exit 1; }
test -f "$OUTBOX5/20260911_170000_fresh/SHIPPED" || { echo "NG: 詰まった箱があると新しい箱が運ばれない"; exit 1; }

# rsync が1箱で失敗しても後続は運ぶ（到達不能のときだけ全体を降りる）
# A per-box rsync failure does not stop the loop; only an unreachable host does
cat > "$TMP/rsync-selective" <<'SH'
#!/usr/bin/env bash
src="${@: -2:1}"; dst="${@: -1}"; dst="${dst#*:}"
case "$src" in *rsyncfail*) exit 23 ;; esac
mkdir -p "$dst"; cp -R "$src"/. "$dst"/
SH
chmod +x "$TMP/rsync-selective"
OUTBOX6="$TMP/outbox6"; INBOX6="$TMP/inbox6"; mkdir -p "$OUTBOX6/20260911_180000_rsyncfail" "$OUTBOX6/20260911_190000_after" "$INBOX6"
for id in 20260911_180000_rsyncfail 20260911_190000_after; do
  echo '{"repository":{"commit":"","dirty":false},"masterData":{"commit":"","dirty":false}}' > "$OUTBOX6/$id/manifest.json"
  touch "$OUTBOX6/$id/READY"
done
OUTBOX_DIR="$OUTBOX6" MACMINI_SSH="stub@host" MACMINI_INBOX="$INBOX6" RSYNC_CMD="$TMP/rsync-selective" SSH_CMD="$TMP/ssh" GIT_CMD=true \
  bash "$HERE/../ship-outbox.sh" 2>"$TMP/rsyncfail.log"
grep -q "rsync 失敗" "$TMP/rsyncfail.log" || { echo "NG: rsync 失敗の理由がログされていない"; exit 1; }
test ! -e "$OUTBOX6/20260911_180000_rsyncfail/SHIPPED" || { echo "NG: rsync 失敗なのに SHIPPED が付いた"; exit 1; }
test -f "$OUTBOX6/20260911_190000_after/SHIPPED" || { echo "NG: rsync 失敗の箱の後続が運ばれていない"; exit 1; }

# 到達不能なら理由をログして何も運ばない
# An unreachable host logs the reason and ships nothing
cat > "$TMP/ssh-dead" <<'SH'
#!/usr/bin/env bash
exit 255
SH
chmod +x "$TMP/ssh-dead"
OUTBOX7="$TMP/outbox7"; INBOX7="$TMP/inbox7"; mkdir -p "$OUTBOX7/20260911_200000_offline" "$INBOX7"
echo '{"repository":{"commit":"","dirty":false},"masterData":{"commit":"","dirty":false}}' > "$OUTBOX7/20260911_200000_offline/manifest.json"
touch "$OUTBOX7/20260911_200000_offline/READY"
OUTBOX_DIR="$OUTBOX7" MACMINI_SSH="stub@host" MACMINI_INBOX="$INBOX7" RSYNC_CMD="$TMP/rsync" SSH_CMD="$TMP/ssh-dead" GIT_CMD=true \
  bash "$HERE/../ship-outbox.sh" 2>"$TMP/offline.log"
grep -q "Mac mini へ到達できない" "$TMP/offline.log" || { echo "NG: 到達不能の理由がログされていない"; exit 1; }
test ! -e "$OUTBOX7/20260911_200000_offline/SHIPPED" || { echo "NG: 到達不能なのに SHIPPED が付いた"; exit 1; }

# C9: bundle 作成の失敗と manifest の読み取り失敗を無音にしない（3状態を箱へ残す）
# C9: neither a failed bundle nor an unreadable manifest is silent; the three states land in the box
cat > "$TMP/git-bundlefail" <<'SH'
#!/usr/bin/env bash
for arg in "$@"; do
  [ "$arg" = "bundle" ] && exit 1        # bundle create だけ失敗させる / only bundle create fails
  [ "$arg" = "merge-base" ] && exit 1    # origin には無いコミット扱い / the commit is not in origin
done
exit 0
SH
chmod +x "$TMP/git-bundlefail"
OUTBOX8="$TMP/outbox8"; INBOX8="$TMP/inbox8"; mkdir -p "$OUTBOX8/20260911_210000_bundlefail" "$OUTBOX8/20260911_220000_badmanifest" "$INBOX8"
echo '{"repository":{"commit":"deadbeef","dirty":false},"masterData":{"commit":"","dirty":false}}' > "$OUTBOX8/20260911_210000_bundlefail/manifest.json"
printf '{ broken' > "$OUTBOX8/20260911_220000_badmanifest/manifest.json"
touch "$OUTBOX8/20260911_210000_bundlefail/READY" "$OUTBOX8/20260911_220000_badmanifest/READY"
OUTBOX_DIR="$OUTBOX8" MACMINI_SSH="stub@host" MACMINI_INBOX="$INBOX8" RSYNC_CMD="$TMP/rsync" SSH_CMD="$TMP/ssh" \
  GIT_CMD="$TMP/git-bundlefail" MOORESTECH_REPO="$TMP/repo" MOORESTECH_MASTER="$TMP/repo" \
  bash "$HERE/../ship-outbox.sh" 2>"$TMP/bundlefail.log"
grep -q "bundle 作成に失敗した" "$TMP/bundlefail.log" || { echo "NG: bundle 作成失敗が無音になっている"; exit 1; }
grep -q "commits.bundle: failed-bundle-create" "$INBOX8/20260911_210000_bundlefail/repo/bundle-status.txt" \
  || { echo "NG: bundle の失敗が箱に残っていない"; exit 1; }
grep -q "manifest の repository.commit を読めない" "$TMP/bundlefail.log" || { echo "NG: manifest 読み取り失敗が無音になっている"; exit 1; }
grep -q "commits.bundle: skipped-no-commit" "$INBOX8/20260911_220000_badmanifest/repo/bundle-status.txt" \
  || { echo "NG: commit 不明が箱に残っていない"; exit 1; }

echo "OK"
