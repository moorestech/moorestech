#!/usr/bin/env bash
# claude と prepare を差し替えて、inbox → runs 移動・result・logs commit を検証する
# Verifies inbox→runs move, result file and the logs commit with claude/prepare stubbed
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
TMP="$(mktemp -d)"; trap 'rm -rf "${TMP:?}"' EXIT
export TMPDIR="$TMP"   # ロックもテスト用一時ディレクトリへ隔離する / keep the lock inside the test temp dir
LOGS="$TMP/logs"; mkdir -p "$LOGS/harness/bug-report/inbox/20260911_120000_aaaa1111" "$LOGS/harness/bug-report/runs"
( cd "$LOGS" && git init -q && git config user.email t@t && git config user.name t && echo x > .gitkeep && git add . && git commit -qm init )
echo '{"description":"x"}' > "$LOGS/harness/bug-report/inbox/20260911_120000_aaaa1111/manifest.json"
touch "$LOGS/harness/bug-report/inbox/20260911_120000_aaaa1111/READY"
# 運搬中の箱は READY を含んでいても掴まない / an in-flight box carries READY too and must be ignored
mkdir -p "$LOGS/harness/bug-report/inbox/20260911_110000_partial.partial"
touch "$LOGS/harness/bug-report/inbox/20260911_110000_partial.partial/READY"

cat > "$TMP/claude" <<'SH'
#!/usr/bin/env bash
echo '{"result":"stub"}'
SH
cat > "$TMP/prepare" <<'SH'
#!/usr/bin/env bash
echo "WORKTREE=/tmp" > "$MOORESTECH_LOGS/harness/bug-report/runs/$1/run.env"
SH
chmod +x "$TMP/claude" "$TMP/prepare"

MOORESTECH_LOGS="$LOGS" CLAUDE_CMD="$TMP/claude" PREPARE_CMD="$TMP/prepare" GIT_PUSH=0 bash "$HERE/../inbox-poller.sh" 2>"$TMP/run1.log"
RUN="$LOGS/harness/bug-report/runs/20260911_120000_aaaa1111"
[ -d "$RUN" ] || { echo "NG: runs へ移動していない"; exit 1; }
[ ! -e "$LOGS/harness/bug-report/inbox/20260911_120000_aaaa1111" ] || { echo "NG: inbox に残っている"; exit 1; }
[ -d "$LOGS/harness/bug-report/inbox/20260911_110000_partial.partial" ] || { echo "NG: .partial を触った"; exit 1; }
[ ! -e "$LOGS/harness/bug-report/runs/20260911_110000_partial.partial" ] || { echo "NG: .partial を処理した"; exit 1; }
grep -q "運搬中のためスキップ" "$TMP/run1.log" || { echo "NG: .partial スキップの理由がログされていない"; exit 1; }
[ -f "$RUN/claude.out.json" ] || { echo "NG: claude 出力が無い"; exit 1; }
grep -q '"status": *"failure"' "$RUN/fix-result.json" || { echo "NG: result 無しの補完が無い"; exit 1; }
# pipefail 下で grep -q に流すと git が SIGPIPE で落ちるため、いったんファイルへ出す
# Under pipefail, piping into grep -q kills git with SIGPIPE, so dump the log to a file first
git -C "$LOGS" log --oneline > "$TMP/logs-commits.txt"
grep -q "bug-report run 20260911_120000_aaaa1111" "$TMP/logs-commits.txt" || { echo "NG: logs commit"; exit 1; }
[ ! -d "$TMP/moorestech-bugreport-poller.lock" ] || { echo "NG: ロックが残っている"; exit 1; }

# 同じ id が runs に残っている状態で再び届いても、二度処理せず入れ子にもしない
# A box that reuses an existing run id is neither processed twice nor nested
mkdir -p "$LOGS/harness/bug-report/inbox/20260911_120000_aaaa1111"
touch "$LOGS/harness/bug-report/inbox/20260911_120000_aaaa1111/READY"
MOORESTECH_LOGS="$LOGS" CLAUDE_CMD="$TMP/claude" PREPARE_CMD="$TMP/prepare" GIT_PUSH=0 bash "$HERE/../inbox-poller.sh" 2>"$TMP/run2.log"
grep -q "二重処理を避けて中断" "$TMP/run2.log" || { echo "NG: 二重処理中断の理由がログされていない"; exit 1; }
[ -d "$LOGS/harness/bug-report/inbox/20260911_120000_aaaa1111" ] || { echo "NG: 重複箱が消えた"; exit 1; }
[ ! -e "$RUN/20260911_120000_aaaa1111" ] || { echo "NG: runs の中へ入れ子にされた"; exit 1; }
git -C "$LOGS" log --oneline > "$TMP/logs-commits2.txt"
[ "$(grep -c "bug-report run 20260911_120000_aaaa1111" "$TMP/logs-commits2.txt")" = "1" ] || { echo "NG: 二重コミット"; exit 1; }
rm -rf "${LOGS:?}/harness/bug-report/inbox/20260911_120000_aaaa1111"

# ロックが取られている間は起動しない（多重起動防止）
# While the lock is held, a second invocation does not start (single flight)
mkdir -p "$LOGS/harness/bug-report/inbox/20260911_140000_cccc3333"
touch "$LOGS/harness/bug-report/inbox/20260911_140000_cccc3333/READY"
mkdir "$TMP/moorestech-bugreport-poller.lock"
MOORESTECH_LOGS="$LOGS" CLAUDE_CMD="$TMP/claude" PREPARE_CMD="$TMP/prepare" GIT_PUSH=0 bash "$HERE/../inbox-poller.sh" 2>"$TMP/run3.log"
grep -q "別のランが進行中" "$TMP/run3.log" || { echo "NG: ロック理由がログされていない"; exit 1; }
[ -d "$LOGS/harness/bug-report/inbox/20260911_140000_cccc3333" ] || { echo "NG: ロック中なのに箱を処理した"; exit 1; }
rmdir "$TMP/moorestech-bugreport-poller.lock"
rm -rf "${LOGS:?}/harness/bug-report/inbox/20260911_140000_cccc3333"

# prepare が隔離 worktree を用意できなかったら、共有のメインワーキングツリーでは走らせない
# When prepare cannot provide an isolated worktree, the run must not fall back to the shared main working tree
cat > "$TMP/prepare-broken" <<'SH'
#!/usr/bin/env bash
exit 1
SH
cat > "$TMP/claude-forbidden" <<'SH'
#!/usr/bin/env bash
touch "$CLAUDE_CALLED_MARKER"
SH
chmod +x "$TMP/prepare-broken" "$TMP/claude-forbidden"
mkdir -p "$LOGS/harness/bug-report/inbox/20260911_150000_dddd4444"
touch "$LOGS/harness/bug-report/inbox/20260911_150000_dddd4444/READY"
CLAUDE_CALLED_MARKER="$TMP/claude-was-called" MOORESTECH_LOGS="$LOGS" CLAUDE_CMD="$TMP/claude-forbidden" \
  PREPARE_CMD="$TMP/prepare-broken" GIT_PUSH=0 bash "$HERE/../inbox-poller.sh" 2>"$TMP/run4.log"
[ ! -e "$TMP/claude-was-called" ] || { echo "NG: worktree 無しで claude を起こした"; exit 1; }
grep -q "隔離 worktree が無いため自動修正ランを起こさない" "$TMP/run4.log" || { echo "NG: 起動見送りの理由がログされていない"; exit 1; }
grep -q '"status": *"failure"' "$LOGS/harness/bug-report/runs/20260911_150000_dddd4444/fix-result.json" || { echo "NG: failure の結果が無い"; exit 1; }

# READY の箱が1つも無いときも理由を出して終わる（無音の no-op を作らない）
# An empty inbox still says why nothing happened; never a silent no-op
MOORESTECH_LOGS="$LOGS" CLAUDE_CMD="$TMP/claude" PREPARE_CMD="$TMP/prepare" GIT_PUSH=0 bash "$HERE/../inbox-poller.sh" 2>"$TMP/run5.log"
grep -q "READY の箱が無いので何もしない" "$TMP/run5.log" || { echo "NG: 空 inbox の理由がログされていない"; exit 1; }

echo OK
