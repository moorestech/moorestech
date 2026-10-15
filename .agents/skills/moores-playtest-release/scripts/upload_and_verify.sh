#!/bin/bash
# ビルド済み run をアップロードし、取り込みロックを握ったまま Windows 通し検証まで回して promotion.md を書く
# Uploads a built run, runs the Windows smoke check under the ingest lock, then writes promotion.md
# Usage: upload_and_verify.sh <run-dir> [--skip-upload]   (--skip-upload は検証だけやり直すとき)
set -u
RUN_DIR="${1:?usage: upload_and_verify.sh <run-dir> [--skip-upload]}"
SKIP_UPLOAD="${2:-}"
LABEL="$(basename "$RUN_DIR")"
HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$HOME/hermes-agent/data/repos/moorestech"
. "$HOME/hermes-agent/data/services/playtest/env.sh"

if [ "$SKIP_UPLOAD" != "--skip-upload" ]; then
  python3 "$HERE/steam_upload_with_vault.py" "$RUN_DIR" </dev/null || { echo "UPLOAD_FAILED=$?"; exit 1; }
fi
python3 "$HERE/validate_staging.py" "$RUN_DIR" || { echo "VALIDATE_FAILED"; exit 1; }

# ingest.sh と同じロックを握り、検証用の報告を本番の取り込みに拾わせない（死んだロックの回収も ingest-lock.sh に任せる）
# Hold the same lock as ingest.sh so the smoke report is not ingested (dead-lock reclaim is left to ingest-lock.sh)
LOCK="$REPO/../moorestech_logs/.git/moorestech-playtest-ingest.lock"
log() { echo "[ingest-lock] $*" >&2; }
. "$REPO/scripts/playtest/lib/ingest-lock.sh"
for _ in $(seq 1 120); do acquire_lock && break; sleep 10; done
[ "$(cat "$LOCK/pid" 2>/dev/null)" = "$$" ] || { echo "LOCK_FAILED"; exit 1; }
trap 'rm -rf "$LOCK"; echo "ingest lock released"' EXIT
echo "ingest lock held pid=$$"
(cd "$REPO" && scripts/playtest/verify-on-windows.sh "$LABEL")
rc=$?
echo "VERIFY_EXIT=$rc"
[ "$rc" -eq 0 ] || exit "$rc"

COMMIT="$(grep -o 'commit=[0-9a-f]\{40\}' "$(dirname "$RUN_DIR")/$LABEL.release.log" | head -1 | cut -d= -f2)"
cat >"$RUN_DIR/promotion.md" <<MD
# moorestech プレイテスト反映手順 ($LABEL)

- コミット: $COMMIT
- Steam アップロード先: playtest-staging（Windows depot と Mac depot を同じビルドに格納）
- 通し検証: Windows は検証機で phase1 / phase2・受け口到達とも合格。Mac 版は自動検証していない（ADR 0071）

## Mac 版の手動確認（playtest 反映前に必須）

1. Apple Silicon の Mac で Steam のベータ \`playtest-staging\` を選び、更新後に Steam から起動する
2. タイトルから新規ワールドへ入り、Tab でインベントリが開くことを確認する
3. 起動に回避操作が要ったか: [ ] 要らなかった / [ ] 要った（手順: ）

## 反映

\`.agents/skills/moores-playtest-release/scripts/promote_playtest.py $RUN_DIR --mac-checked\` で playtest へ反映し、出力の Build ID をテスターへ告知する。
MD
echo "promotion: $RUN_DIR/promotion.md"
