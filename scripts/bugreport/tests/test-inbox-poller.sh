#!/usr/bin/env bash
# claude・prepare・canon を差し替えて、inbox → runs 移動・result・logs commit・重複箱の隔離・canon 起点の起動を検証する
# Verifies inbox→runs move, result file, logs commit, duplicate quarantine and the canon-rooted launch with claude/prepare/canon stubbed
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
TMP="$(mktemp -d)"; trap 'rm -rf "${TMP:?}"' EXIT
export TMPDIR="$TMP"   # ロックもテスト用一時ディレクトリへ隔離する / keep the lock inside the test temp dir
LOGS="$TMP/logs"; INBOX="$LOGS/harness/bug-report/inbox"; RUNS="$LOGS/harness/bug-report/runs"
mkdir -p "$RUNS"
( cd "$LOGS" && git init -q && git config user.email t@t && git config user.name t && echo x > .gitkeep && git add . && git commit -qm init )
# 箱は必ず遠隔実行の印つきで作る。印が無い箱は不明として遮断される（ADR 0072）
# Every box carries the remote-exec mark; a box without one is held back as unknown (ADR 0072)
ready_box() {
  mkdir -p "$INBOX/$1"
  echo '{"description":"x","remoteExec":{"state":"Disabled","ledgerFiles":[]}}' > "$INBOX/$1/manifest.json"
  touch "$INBOX/$1/READY"
}
ready_box 20260911_120000_aaaa1111
# 運搬中の箱は READY を含んでいても掴まない / an in-flight box carries READY too and must be ignored
mkdir -p "$INBOX/20260911_110000_partial.partial"
touch "$INBOX/20260911_110000_partial.partial/READY"

cat > "$TMP/claude" <<'SH'
#!/usr/bin/env bash
pwd > "$CLAUDE_PWD_FILE"
printf '%s\n' "$@" > "$CLAUDE_ARGS_FILE"
echo '{"result":"stub"}'
SH
cat > "$TMP/prepare" <<'SH'
#!/usr/bin/env bash
echo "WORKTREE=/tmp" > "$MOORESTECH_LOGS/harness/bug-report/runs/$1/run.env"
SH
# canon_setup の役目は SHA 固定ピンのパスを返すこと。テストでは実 repo を触らずパスだけ返す
# canon_setup's job is to return the SHA-pinned path; here it returns a path without touching a real repo
CANON="$TMP/canon"; mkdir -p "$CANON/.agents/skills/bug-report-auto-fix"
touch "$CANON/.agents/skills/bug-report-auto-fix/SKILL.md"
cat > "$TMP/canon-setup" <<SH
#!/usr/bin/env bash
echo '{"canon": "$CANON"}'
SH
chmod +x "$TMP/claude" "$TMP/prepare" "$TMP/canon-setup"
export CLAUDE_PWD_FILE="$TMP/claude-pwd.txt" CLAUDE_ARGS_FILE="$TMP/claude-args.txt"

# 後から渡した代入が勝つ（env は左から順に適用する）/ later assignments win because env applies them in order
poll() {
  env MOORESTECH_LOGS="$LOGS" CLAUDE_CMD="$TMP/claude" PREPARE_CMD="$TMP/prepare" \
    CANON_SETUP_CMD="$TMP/canon-setup" GIT_PUSH=0 "$@" bash "$HERE/../inbox-poller.sh"
}

poll 2>"$TMP/run1.log"
RUN="$RUNS/20260911_120000_aaaa1111"
[ -d "$RUN" ] || { echo "NG: runs へ移動していない"; exit 1; }
[ ! -e "$INBOX/20260911_120000_aaaa1111" ] || { echo "NG: inbox に残っている"; exit 1; }
[ -d "$INBOX/20260911_110000_partial.partial" ] || { echo "NG: .partial を触った"; exit 1; }
[ ! -e "$RUNS/20260911_110000_partial.partial" ] || { echo "NG: .partial を処理した"; exit 1; }
grep -q "運搬中のためスキップ" "$TMP/run1.log" || { echo "NG: .partial スキップの理由がログされていない"; exit 1; }
[ -f "$RUN/claude.out.json" ] || { echo "NG: claude 出力が無い"; exit 1; }
grep -q '"status": *"failure"' "$RUN/fix-result.json" || { echo "NG: result 無しの補完が無い"; exit 1; }
grep -Eq '"finishedAt": *"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z"' "$RUN/fix-result.json" || { echo "NG: 補完に finishedAt が無い"; exit 1; }
# 実行制御の正本は canon。cwd が修正対象 worktree だと、報告時の版のスキル・hook で走ってしまう
# The run-control source is the canon; a cwd of the fix-target worktree would run the report-era skill and hooks
[ "$(cat "$CLAUDE_PWD_FILE")" = "$CANON" ] || { echo "NG: canon を cwd にしていない: $(cat "$CLAUDE_PWD_FILE")"; exit 1; }
grep -qx -- "--add-dir" "$CLAUDE_ARGS_FILE" || { echo "NG: 修正先 worktree が --add-dir で渡されていない"; exit 1; }
grep -qx -- "/tmp" "$CLAUDE_ARGS_FILE" || { echo "NG: --add-dir の値が worktree でない"; exit 1; }
# pipefail 下で grep -q に流すと git が SIGPIPE で落ちるため、いったんファイルへ出す
# Under pipefail, piping into grep -q kills git with SIGPIPE, so dump the log to a file first
git -C "$LOGS" log --oneline > "$TMP/logs-commits.txt"
grep -q "bug-report run 20260911_120000_aaaa1111" "$TMP/logs-commits.txt" || { echo "NG: logs commit"; exit 1; }
[ ! -d "$TMP/moorestech-bugreport-poller.lock" ] || { echo "NG: ロックが残っている"; exit 1; }

# 同じ id が runs に残っている状態で再び届いても、二度処理せず入れ子にもせず、後続の箱を止めない
# A box reusing an existing run id is neither processed twice nor nested, and it must not block the boxes behind it
ready_box 20260911_120000_aaaa1111
ready_box 20260911_130000_bbbb2222
poll 2>"$TMP/run2.log"
grep -q "隔離した" "$TMP/run2.log" || { echo "NG: 重複箱の隔離理由がログされていない"; exit 1; }
[ -d "$INBOX/.duplicate/20260911_120000_aaaa1111" ] || { echo "NG: 重複箱が隔離先に無い"; exit 1; }
[ ! -e "$INBOX/20260911_120000_aaaa1111" ] || { echo "NG: 重複箱が inbox に残っている"; exit 1; }
[ ! -e "$RUN/20260911_120000_aaaa1111" ] || { echo "NG: runs の中へ入れ子にされた"; exit 1; }
[ -d "$RUNS/20260911_130000_bbbb2222" ] || { echo "NG: 重複箱の後ろの箱が処理されていない"; exit 1; }
git -C "$LOGS" log --oneline > "$TMP/logs-commits2.txt"
[ "$(grep -c "bug-report run 20260911_120000_aaaa1111" "$TMP/logs-commits2.txt")" = "1" ] || { echo "NG: 二重コミット"; exit 1; }

# 隔離先にも同名がある箱は動かさずに飛ばす（人が中身を見るまで残す）
# A box whose name already exists in quarantine is left untouched and skipped until a human looks at it
ready_box 20260911_120000_aaaa1111
poll 2>"$TMP/run2b.log"
grep -q "触らず次の候補へ" "$TMP/run2b.log" || { echo "NG: 隔離先衝突の理由がログされていない"; exit 1; }
[ -d "$INBOX/20260911_120000_aaaa1111" ] || { echo "NG: 隔離先衝突の箱を消した"; exit 1; }
rm -rf "${INBOX:?}/20260911_120000_aaaa1111"

# ロックが取られている間は起動しない（多重起動防止）
# While the lock is held, a second invocation does not start (single flight)
ready_box 20260911_140000_cccc3333
mkdir "$TMP/moorestech-bugreport-poller.lock"
poll 2>"$TMP/run3.log"
grep -q "別のランが進行中" "$TMP/run3.log" || { echo "NG: ロック理由がログされていない"; exit 1; }
[ -d "$INBOX/20260911_140000_cccc3333" ] || { echo "NG: ロック中なのに箱を処理した"; exit 1; }
rmdir "$TMP/moorestech-bugreport-poller.lock"
rm -rf "${INBOX:?}/20260911_140000_cccc3333"

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
ready_box 20260911_150000_dddd4444
poll CLAUDE_CALLED_MARKER="$TMP/claude-was-called" CLAUDE_CMD="$TMP/claude-forbidden" \
  PREPARE_CMD="$TMP/prepare-broken" 2>"$TMP/run4.log"
[ ! -e "$TMP/claude-was-called" ] || { echo "NG: worktree 無しで claude を起こした"; exit 1; }
grep -q "隔離 worktree が無いため自動修正ランを起こさない" "$TMP/run4.log" || { echo "NG: 起動見送りの理由がログされていない"; exit 1; }
grep -q '"status": *"failure"' "$RUNS/20260911_150000_dddd4444/fix-result.json" || { echo "NG: failure の結果が無い"; exit 1; }
grep -q '"finishedAt": *"' "$RUNS/20260911_150000_dddd4444/fix-result.json" || { echo "NG: failure 補完に finishedAt が無い"; exit 1; }

# canon にスキルが無い（master 未マージ）なら、修正対象 worktree の版で走らせずに理由を残して止まる
# When the canon lacks the skill (not yet merged to master), the run stops with a reason instead of using the fix-target worktree's revision
cat > "$TMP/canon-setup-empty" <<SH
#!/usr/bin/env bash
mkdir -p "$TMP/canon-empty"
echo '{"canon": "$TMP/canon-empty"}'
SH
chmod +x "$TMP/canon-setup-empty"
ready_box 20260911_170000_ffff6666
poll CLAUDE_CALLED_MARKER="$TMP/claude-was-called-canon" CLAUDE_CMD="$TMP/claude-forbidden" \
  CANON_SETUP_CMD="$TMP/canon-setup-empty" 2>"$TMP/run7.log"
[ ! -e "$TMP/claude-was-called-canon" ] || { echo "NG: canon 不備で claude を起こした"; exit 1; }
grep -q "が無い（このスキルがまだ master に入っていない）" "$TMP/run7.log" || { echo "NG: canon 不備の理由がログされていない"; exit 1; }
grep -q '"status": *"failure"' "$RUNS/20260911_170000_ffff6666/fix-result.json" || { echo "NG: canon 不備の failure 結果が無い"; exit 1; }
grep -q '"finishedAt": *"' "$RUNS/20260911_170000_ffff6666/fix-result.json" || { echo "NG: failure 補完に finishedAt が無い"; exit 1; }

# canon_setup 自体が失敗したときも同じく起こさない / A failing canon_setup blocks the launch just the same
cat > "$TMP/canon-setup-broken" <<'SH'
#!/usr/bin/env bash
echo "boom" >&2
exit 1
SH
chmod +x "$TMP/canon-setup-broken"
ready_box 20260911_180000_9999aaaa
poll CLAUDE_CALLED_MARKER="$TMP/claude-was-called-canon2" CLAUDE_CMD="$TMP/claude-forbidden" \
  CANON_SETUP_CMD="$TMP/canon-setup-broken" 2>"$TMP/run8.log"
[ ! -e "$TMP/claude-was-called-canon2" ] || { echo "NG: canon_setup 失敗で claude を起こした"; exit 1; }
grep -q "canon worktree を用意できなかった" "$TMP/run8.log" || { echo "NG: canon_setup 失敗の理由がログされていない"; exit 1; }
grep -q "boom" "$RUNS/20260911_180000_9999aaaa/canon.err.log" || { echo "NG: canon_setup の stderr が残っていない"; exit 1; }

# READY の箱が1つも無いときも理由を出して終わる（無音の no-op を作らない）
# An empty inbox still says why nothing happened; never a silent no-op
poll 2>"$TMP/run5.log"
grep -q "READY の箱が無いので何もしない" "$TMP/run5.log" || { echo "NG: 空 inbox の理由がログされていない"; exit 1; }

# push の失敗を無音で 0 に潰さない（記録が private remote へ届いていないことを必ず言う）
# A failed push is never swallowed; the run must say the record did not reach the private remote
ready_box 20260911_160000_eeee5555
poll GIT_PUSH=1 2>"$TMP/run6.log"
grep -q "logs push 失敗" "$TMP/run6.log" || { echo "NG: push 失敗の理由がログされていない"; exit 1; }
git -C "$LOGS" log --oneline > "$TMP/logs-commits3.txt"
grep -q "bug-report run 20260911_160000_eeee5555" "$TMP/logs-commits3.txt" || { echo "NG: push 失敗で commit まで失われた"; exit 1; }

# 遠隔実行が有効/不明の箱はランを起こさず退避する。読めない manifest も無効と名乗らせない
# A box with remote execution enabled or unknown is held instead of run; an unreadable manifest never claims "disabled"
mkdir -p "$INBOX/20260911_190000_rex11111"
echo '{"description":"x","remoteExec":{"state":"Enabled","ledgerFiles":[]}}' > "$INBOX/20260911_190000_rex11111/manifest.json"
touch "$INBOX/20260911_190000_rex11111/READY"
poll CLAUDE_CALLED_MARKER="$TMP/claude-was-called-rex" CLAUDE_CMD="$TMP/claude-forbidden" 2>"$TMP/run9.log"
[ ! -e "$TMP/claude-was-called-rex" ] || { echo "NG: 遠隔実行ありの箱で claude を起こした"; exit 1; }
grep -q "遠隔実行が有効/不明のためランを起こさず退避した" "$TMP/run9.log" || { echo "NG: 遮断理由がログされていない"; exit 1; }
[ -d "$INBOX/.remote-exec-held/20260911_190000_rex11111" ] || { echo "NG: 退避先に箱が無い"; exit 1; }
[ ! -e "$RUNS/20260911_190000_rex11111" ] || { echo "NG: runs へ移動した"; exit 1; }

# 遮断した箱の後ろにある箱は同じ回で処理する（1件の滞留が以後のランを止めない）
# A box behind a held one is processed in the same pass, so one held box never stalls later runs
mkdir -p "$INBOX/20260911_210000_rex22222"
echo '{"description":"x","remoteExec":{"state":"Unknown","unknownReason":"読めなかった","ledgerFiles":[]}}' > "$INBOX/20260911_210000_rex22222/manifest.json"
touch "$INBOX/20260911_210000_rex22222/READY"
ready_box 20260911_220000_after111
poll 2>"$TMP/run11.log"
[ -d "$INBOX/.remote-exec-held/20260911_210000_rex22222" ] || { echo "NG: 不明の箱が退避されていない"; exit 1; }
[ -d "$RUNS/20260911_220000_after111" ] || { echo "NG: 遮断箱の後ろの箱が処理されていない"; exit 1; }

mkdir -p "$INBOX/20260911_200000_nomani11"
touch "$INBOX/20260911_200000_nomani11/READY"
poll CLAUDE_CALLED_MARKER="$TMP/claude-was-called-nomani" CLAUDE_CMD="$TMP/claude-forbidden" 2>"$TMP/run10.log"
[ ! -e "$TMP/claude-was-called-nomani" ] || { echo "NG: manifest の無い箱で claude を起こした"; exit 1; }
grep -q "manifest を読めない" "$TMP/run10.log" || { echo "NG: 読めない manifest の理由がログされていない"; exit 1; }
[ -d "$INBOX/.remote-exec-held/20260911_200000_nomani11" ] || { echo "NG: 読めない箱が退避されていない"; exit 1; }

echo OK
