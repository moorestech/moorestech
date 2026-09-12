#!/usr/bin/env bash
# 一時 bare repo と作業クローンで prepare-run.sh の git 操作列と world/ 組み立てを検証する
# Verifies prepare-run.sh's git sequence and world/ assembly using a temp bare repo and clone
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
TMP="$(mktemp -d)"; trap 'rm -rf "${TMP:?}"' EXIT

# origin と作業クローン（moorestech 相当）を作る。既定ブランチ名は master に固定する
# Create an origin and a working clone (stands in for moorestech), pinning the default branch to master
git -c init.defaultBranch=master init -q --bare "$TMP/origin.git"
git -c init.defaultBranch=master clone -q "$TMP/origin.git" "$TMP/repo"
( cd "$TMP/repo" && git config user.email t@t && git config user.name t && mkdir -p moorestech_client/Library && echo lib > moorestech_client/Library/x && echo a > a.txt && git add a.txt && git commit -qm base && git push -q origin master )
# 報告コミットは未push（bundle 経由で届く）
# The report commit is unpushed (arrives via bundle)
( cd "$TMP/repo" && echo b > b.txt && git add b.txt && git commit -qm report )
REPORT_COMMIT="$(git -C "$TMP/repo" rev-parse HEAD)"
git -C "$TMP/repo" update-ref refs/bugreport/r1 "$REPORT_COMMIT"
git -C "$TMP/repo" bundle create -q "$TMP/commits.bundle" "origin/master..refs/bugreport/r1"
git -C "$TMP/repo" update-ref -d refs/bugreport/r1

# master data 相当
# Stand-in for master data
git init -q "$TMP/master"; ( cd "$TMP/master" && git config user.email t@t && git config user.name t && mkdir -p server_v8/mods && echo m > server_v8/mods/x && git add . && git commit -qm m )
MASTER_COMMIT="$(git -C "$TMP/master" rev-parse HEAD)"

RUN="$TMP/runs/r1"; mkdir -p "$RUN/repo" "$RUN/snapshots" "$RUN/world"
cp "$TMP/commits.bundle" "$RUN/repo/commits.bundle"
printf 'diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n@@ -1 +1 @@\n-a\n+changed\n' > "$RUN/repo/head.diff"
mkdir -p "$RUN/repo/untracked/new" && echo n > "$RUN/repo/untracked/new/file.txt"
echo '{"seed":0}' > "$RUN/world/world.json"; echo '{}' > "$RUN/world/map.json"
echo '{"currentTick":600}' > "$RUN/snapshots/tick_600.json"; echo '{"currentTick":1200}' > "$RUN/snapshots/tick_1200.json"
cat > "$RUN/manifest.json" <<JSON
{"repository":{"commit":"$REPORT_COMMIT","branch":"feature/x","dirty":true},"masterData":{"commit":"$MASTER_COMMIT","dirty":false},"serverData":{"path":"/report/moorestech_master/server_v8","relativeTo":"masterData","relativePath":"server_v8"},"snapshotTicks":[600,1200]}
JSON

MOORESTECH_REPO="$TMP/repo" MOORESTECH_WORKTREES="$TMP/wt" MOORESTECH_MASTER="$TMP/master" MOORESTECH_MASTER_WORKTREES="$TMP/mwt" MOORESTECH_LOGS="$TMP" \
  bash "$HERE/../prepare-run.sh" r1

. "$RUN/run.env"
[ "$REPORT_COMMIT" = "$(git -C "$WORKTREE" rev-parse HEAD)" ] || { echo "NG: HEAD が報告コミットでない"; exit 1; }
[ "$(git -C "$WORKTREE" rev-parse --abbrev-ref HEAD)" = "bugfix/r1" ] || { echo "NG: ブランチ"; exit 1; }
[ "$(cat "$WORKTREE/a.txt")" = "changed" ] || { echo "NG: diff 未適用"; exit 1; }
[ -f "$WORKTREE/new/file.txt" ] || { echo "NG: 未追跡コピー"; exit 1; }
[ -f "$WORKTREE/b.txt" ] || { echo "NG: bundle のコミットが取り込まれていない"; exit 1; }
[ -f "$WORKTREE/moorestech_client/Library/x" ] || { echo "NG: Library コピー"; exit 1; }
[ "$MASTER_DIR" = "$TMP/mwt/bugfix-r1/server_v8" ] || { echo "NG: MASTER_DIR=$MASTER_DIR"; exit 1; }
[ "$SERVER_DATA_DIR" = "$TMP/mwt/bugfix-r1/server_v8" ] || { echo "NG: SERVER_DATA_DIR=$SERVER_DATA_DIR"; exit 1; }
[ "$LATEST_TICK" = "1200" ] || { echo "NG: LATEST_TICK=$LATEST_TICK"; exit 1; }
[ "$(cat "$WORLD_DIR/save.json")" = '{"currentTick":1200}' ] || { echo "NG: save.json"; exit 1; }
[ "$COMMIT_MISSING" = "0" ] && [ "$DIFF_APPLY_FAILED" = "0" ] || { echo "NG: フラグ"; exit 1; }

# 二重準備は既存 worktree を消さずに失敗する
# A second preparation fails instead of destroying the existing worktree
if MOORESTECH_REPO="$TMP/repo" MOORESTECH_WORKTREES="$TMP/wt" MOORESTECH_MASTER="$TMP/master" MOORESTECH_MASTER_WORKTREES="$TMP/mwt" MOORESTECH_LOGS="$TMP" \
    bash "$HERE/../prepare-run.sh" r1 2>"$TMP/second.log"; then
  echo "NG: worktree が既にあるのに成功した"; exit 1
fi
grep -q "二重準備を避けて中断" "$TMP/second.log" || { echo "NG: 中断理由がログされていない"; exit 1; }
[ -f "$WORKTREE/b.txt" ] || { echo "NG: 既存 worktree が壊された"; exit 1; }

# リポジトリ配下のサーバーデータ（テスト用modセット等）は本体 worktree の側で解決する
# Server data under the code repository (a test mod set, say) resolves inside the code worktree
RUN1B="$TMP/runs/r1b"; mkdir -p "$RUN1B"
( cd "$TMP/repo" && mkdir -p moorestech_client/TestServerData/mods && echo m > moorestech_client/TestServerData/mods/x && git add moorestech_client/TestServerData && git commit -qm testserverdata && git push -q origin HEAD:master )
REPORT_COMMIT_1B="$(git -C "$TMP/repo" rev-parse HEAD)"
cat > "$RUN1B/manifest.json" <<JSON
{"repository":{"commit":"$REPORT_COMMIT_1B","branch":"feature/x","dirty":false},"masterData":{"commit":"$MASTER_COMMIT","dirty":false},"serverData":{"path":"/report/moorestech_client/TestServerData","relativeTo":"repository","relativePath":"moorestech_client/TestServerData"},"snapshotTicks":[600]}
JSON
MOORESTECH_REPO="$TMP/repo" MOORESTECH_WORKTREES="$TMP/wt" MOORESTECH_MASTER="$TMP/master" MOORESTECH_MASTER_WORKTREES="$TMP/mwt" MOORESTECH_LOGS="$TMP" \
  bash "$HERE/../prepare-run.sh" r1b 2>"$TMP/r1b.log"
( . "$RUN1B/run.env"; [ "$SERVER_DATA_DIR" = "$TMP/wt/bugfix-r1b/moorestech_client/TestServerData" ] ) || { echo "NG: r1b の SERVER_DATA_DIR"; cat "$TMP/r1b.log"; exit 1; }

# 受け側に無いサーバーデータを黙って別のマスタで代用しない。空にして理由をログする
# Server data absent on the receiving side is never silently swapped for other masters; it stays empty with a logged reason
RUN1C="$TMP/runs/r1c"; mkdir -p "$RUN1C"
cat > "$RUN1C/manifest.json" <<JSON
{"repository":{"commit":"$REPORT_COMMIT","branch":"feature/x","dirty":false},"masterData":{"commit":"$MASTER_COMMIT","dirty":false},"serverData":{"path":"/report/elsewhere","relativeTo":"absolute","relativePath":""},"snapshotTicks":[600]}
JSON
MOORESTECH_REPO="$TMP/repo" MOORESTECH_WORKTREES="$TMP/wt" MOORESTECH_MASTER="$TMP/master" MOORESTECH_MASTER_WORKTREES="$TMP/mwt" MOORESTECH_LOGS="$TMP" \
  bash "$HERE/../prepare-run.sh" r1c 2>"$TMP/r1c.log"
grep -q "serverData がリポジトリの外を指している" "$TMP/r1c.log" || { echo "NG: 解決できない serverData の理由がログされていない"; exit 1; }
grep -q "決定性検査は行えない" "$TMP/r1c.log" || { echo "NG: 決定性検査を行えない旨がログされていない"; exit 1; }
( . "$RUN1C/run.env"; [ -z "$SERVER_DATA_DIR" ] && [ -n "$MASTER_DIR" ] ) || { echo "NG: r1c の SERVER_DATA_DIR が空でない"; exit 1; }

# 報告コミットが手に入らない場合は origin/master を土台にしてフラグを立てる
# When the report commit is unavailable, fall back to origin/master and raise the flag
RUN2="$TMP/runs/r2"; mkdir -p "$RUN2/repo" "$RUN2/snapshots"
echo '{"currentTick":300}' > "$RUN2/snapshots/tick_300.json"
cat > "$RUN2/manifest.json" <<JSON
{"repository":{"commit":"0000000000000000000000000000000000000000","branch":"feature/y","dirty":false},"masterData":{"commit":"","dirty":false},"snapshotTicks":[300]}
JSON
MOORESTECH_REPO="$TMP/repo" MOORESTECH_WORKTREES="$TMP/wt" MOORESTECH_MASTER="$TMP/master" MOORESTECH_MASTER_WORKTREES="$TMP/mwt" MOORESTECH_LOGS="$TMP" \
  bash "$HERE/../prepare-run.sh" r2 2>"$TMP/r2.log"
grep -q "報告コミットが無いため" "$TMP/r2.log" || { echo "NG: 欠損コミットの理由がログされていない"; exit 1; }
( . "$RUN2/run.env"; [ "$COMMIT_MISSING" = "1" ] && [ -z "$MASTER_DIR" ] ) || { echo "NG: r2 のフラグ/MASTER_DIR"; exit 1; }

# 添付が欠けた箱（snapshotTicks 空・repository/masterData 欠落・missing 申告あり）でも止まらず理由を全部ログする
# A box with missing attachments (empty snapshotTicks, no repository/masterData, declared missing items) must not stop the run
RUN3="$TMP/runs/r3"; mkdir -p "$RUN3"
cat > "$RUN3/manifest.json" <<'JSON'
{"snapshotTicks":[],"missing":[{"item":"serverSnapshot","reason":"サーバーが応答しなかった"},{"item":"video","reason":"リングが始まっていない"}]}
JSON
ENVS=(MOORESTECH_REPO="$TMP/repo" MOORESTECH_WORKTREES="$TMP/wt" MOORESTECH_MASTER="$TMP/master" MOORESTECH_MASTER_WORKTREES="$TMP/mwt" MOORESTECH_LOGS="$TMP")
env "${ENVS[@]}" bash "$HERE/../prepare-run.sh" r3 2>"$TMP/r3.log" || { echo "NG: 欠損した箱で prepare が落ちた"; cat "$TMP/r3.log"; exit 1; }
[ -f "$RUN3/run.env" ] || { echo "NG: r3 の run.env が無い"; exit 1; }
( . "$RUN3/run.env"; [ -z "$LATEST_TICK" ] && [ -d "$WORKTREE" ] && [ "$COMMIT_MISSING" = "1" ] && [ -z "$MASTER_DIR" ] && [ -z "$SERVER_DATA_DIR" ] ) || { echo "NG: r3 の run.env 内容"; exit 1; }
for phrase in "snapshotTicks が空" "manifest に serverData が無い" "報告側が欠損を申告している: serverSnapshot" "報告側が欠損を申告している: video" \
              "repository.commit は空として" "masterData.commit は空として" "スナップショットの tick が無いため" \
              "ワールド定義が箱に無い: world.json" "ワールド定義が箱に無い: map.json" "未追跡ファイルが箱に無い"; do
  grep -q "$phrase" "$TMP/r3.log" || { echo "NG: 欠損の理由がログされていない: $phrase"; exit 1; }
done

# manifest.json 自体が壊れていても（無い場合も）run.env まで書いて続行する
# Even a corrupt (or absent) manifest.json still yields a run.env and keeps going
RUN4="$TMP/runs/r4"; mkdir -p "$RUN4"; printf '{ broken' > "$RUN4/manifest.json"
env "${ENVS[@]}" bash "$HERE/../prepare-run.sh" r4 2>"$TMP/r4.log" || { echo "NG: 壊れた manifest で prepare が落ちた"; cat "$TMP/r4.log"; exit 1; }
grep -q "manifest.json を読めない" "$TMP/r4.log" || { echo "NG: manifest 破損の理由がログされていない"; exit 1; }
( . "$RUN4/run.env"; [ -d "$WORKTREE" ] ) || { echo "NG: r4 の worktree が無い"; exit 1; }

RUN5="$TMP/runs/r5"; mkdir -p "$RUN5"
env "${ENVS[@]}" bash "$HERE/../prepare-run.sh" r5 2>"$TMP/r5.log" || { echo "NG: manifest 不在で prepare が落ちた"; cat "$TMP/r5.log"; exit 1; }
grep -q "manifest.json を読めない" "$TMP/r5.log" || { echo "NG: manifest 不在の理由がログされていない"; exit 1; }
[ -f "$RUN5/run.env" ] || { echo "NG: r5 の run.env が無い"; exit 1; }

# C10: master 側も差分・未追跡ファイルを復元し、head.diff 不在は理由とフラグを残す
# C10: the master side restores its diff and untracked files too, and an absent head.diff leaves a reason and a flag
RUN6="$TMP/runs/r6"; mkdir -p "$RUN6/repo/master-untracked/added"
printf 'diff --git a/server_v8/mods/x b/server_v8/mods/x\n--- a/server_v8/mods/x\n+++ b/server_v8/mods/x\n@@ -1 +1 @@\n-m\n+changed\n' > "$RUN6/repo/master.diff"
echo '{"recipe":1}' > "$RUN6/repo/master-untracked/added/recipe.json"
cat > "$RUN6/manifest.json" <<JSON
{"repository":{"commit":"$REPORT_COMMIT_1B","branch":"feature/x","dirty":false},"masterData":{"commit":"$MASTER_COMMIT","dirty":true},"snapshotTicks":[600]}
JSON
env "${ENVS[@]}" bash "$HERE/../prepare-run.sh" r6 2>"$TMP/r6.log"
[ "$(cat "$TMP/mwt/bugfix-r6/server_v8/mods/x")" = "changed" ] || { echo "NG: master.diff が当たっていない"; cat "$TMP/r6.log"; exit 1; }
[ -f "$TMP/mwt/bugfix-r6/added/recipe.json" ] || { echo "NG: master-untracked が復元されていない"; exit 1; }
grep -q "head.diff が無い/空" "$TMP/r6.log" || { echo "NG: head.diff 不在の理由がログされていない"; exit 1; }
( . "$RUN6/run.env"; [ "$MASTER_DIFF_APPLY_FAILED" = "0" ] && [ "$MASTER_DIFF_ABSENT" = "0" ] && [ "$MASTER_UNTRACKED_FAILED" = "0" ] && [ "$DIFF_ABSENT" = "1" ] ) \
  || { echo "NG: r6 のフラグ"; cat "$RUN6/run.env"; exit 1; }

# master.diff が当たらない・bundle が壊れている場合は無音で落とさずフラグと理由を残す
# A master.diff that does not apply and a corrupt bundle both leave a flag and a logged reason, never silence
RUN7="$TMP/runs/r7"; mkdir -p "$RUN7/repo"
printf 'diff --git a/server_v8/mods/none b/server_v8/mods/none\n--- a/server_v8/mods/none\n+++ b/server_v8/mods/none\n@@ -1 +1 @@\n-a\n+b\n' > "$RUN7/repo/master.diff"
printf 'not a bundle' > "$RUN7/repo/commits.bundle"
cat > "$RUN7/manifest.json" <<JSON
{"repository":{"commit":"$REPORT_COMMIT_1B","branch":"feature/x","dirty":false},"masterData":{"commit":"$MASTER_COMMIT","dirty":true},"snapshotTicks":[600]}
JSON
env "${ENVS[@]}" bash "$HERE/../prepare-run.sh" r7 2>"$TMP/r7.log"
grep -q "master.diff の適用に失敗" "$TMP/r7.log" || { echo "NG: master.diff 失敗の理由がログされていない"; exit 1; }
grep -q "commits.bundle を取り込めなかった" "$TMP/r7.log" || { echo "NG: bundle 取り込み失敗の理由がログされていない"; exit 1; }
grep -q "マスタの未追跡ファイルが箱に無い" "$TMP/r7.log" || { echo "NG: master-untracked 不在の理由がログされていない"; exit 1; }
( . "$RUN7/run.env"; [ "$MASTER_DIFF_APPLY_FAILED" = "1" ] ) || { echo "NG: r7 の MASTER_DIFF_APPLY_FAILED"; cat "$RUN7/run.env"; exit 1; }

echo OK
