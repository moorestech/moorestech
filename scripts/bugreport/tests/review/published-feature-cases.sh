#!/usr/bin/env bash
# feature公開前の独立cloneで、bundle無しの両SHAを復元する
# Restore both SHAs without bundles using independent clones made before feature publication
git clone -q "$TMP/origin.git" "$TMP/feature-receiver"
git clone -q "$TMP/master-origin.git" "$TMP/feature-master-receiver"
git -C "$TMP/master" config user.email t@t
git -C "$TMP/master" config user.name Test
echo feature > "$TMP/master/server_v8/mods/feature"
git -C "$TMP/master" add server_v8/mods/feature
git -C "$TMP/master" commit -qm feature
feature_master_commit="$(git -C "$TMP/master" rev-parse HEAD)"
git -C "$TMP/repo" push -q origin HEAD:refs/heads/feature/published
git -C "$TMP/master" push -q origin HEAD:refs/heads/feature/published

# 受信側に対象SHAが無いことを先に確認する
# First confirm neither receiver contains its target SHA
if git -C "$TMP/feature-receiver" cat-file -e "$REPORT_COMMIT^{commit}" 2>/dev/null \
  || git -C "$TMP/feature-master-receiver" cat-file -e "$feature_master_commit^{commit}" 2>/dev/null; then
  echo "NG: 公開前cloneにfeature SHAが存在する"; exit 1
fi
feature_run="$TMP/runs/published-feature"
mkdir -p "$feature_run"
cat > "$feature_run/manifest.json" <<JSON
{"repository":{"commit":"$REPORT_COMMIT"},"masterData":{"commit":"$feature_master_commit"},"snapshotTicks":[]}
JSON
MOORESTECH_REPO="$TMP/feature-receiver" MOORESTECH_WORKTREES="$TMP/feature-wt" \
  MOORESTECH_MASTER="$TMP/feature-master-receiver" MOORESTECH_MASTER_WORKTREES="$TMP/feature-mwt" MOORESTECH_LOGS="$TMP" \
  bash "$HERE/../prepare-run.sh" published-feature 2>"$TMP/published-feature.log"

# フラグだけでなく両worktreeのHEADも報告SHAと一致させる
# Check both restored worktree HEADs as well as the failure flags
(
  . "$feature_run/run.env"
  [ "$COMMIT_MISSING" = "0" ] && [ "$MASTER_FAILED" = "0" ] \
    && [ "$(git -C "$WORKTREE" rev-parse HEAD)" = "$REPORT_COMMIT" ] \
    && [ "$(git -C "$TMP/feature-mwt/bugfix-published-feature" rev-parse HEAD)" = "$feature_master_commit" ]
) || { echo "NG: bundle無しfeatureの復元"; cat "$TMP/published-feature.log"; exit 1; }

# 後続の既存fixtureは元のmasterコミットを使う
# Leave the original master commit checked out for subsequent fixture cases
git -C "$TMP/master" checkout -q --detach "$MASTER_COMMIT"
