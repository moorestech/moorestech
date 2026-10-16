#!/usr/bin/env bash
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
git -c init.defaultBranch=master init -q --bare "$TMP/master-origin.git"
git -c init.defaultBranch=master clone -q "$TMP/master-origin.git" "$TMP/master"
( cd "$TMP/master" && git config user.email t@t && git config user.name t && mkdir -p server_v8/mods && echo m > server_v8/mods/x && git add . && git commit -qm m && git push -q origin master )
MASTER_COMMIT="$(git -C "$TMP/master" rev-parse HEAD)"
