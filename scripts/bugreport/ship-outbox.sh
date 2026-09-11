#!/usr/bin/env bash
# outbox の READY 付き箱を Mac mini の inbox へ rsync で運ぶ（ADR 0057）。届かなければ何もしない
# Ships READY boxes from the outbox to the Mac mini inbox via rsync (ADR 0057); does nothing when unreachable
set -euo pipefail

ENV_FILE="${BUGREPORT_SHIPPER_ENV:-$HOME/.config/moorestech/bugreport-shipper.env}"
[ -f "$ENV_FILE" ] && . "$ENV_FILE"

OUTBOX_DIR="${OUTBOX_DIR:-$HOME/Library/Application Support/moorestech/BugReports/outbox}"
MACMINI_SSH="${MACMINI_SSH:?MACMINI_SSH (user@tailscale-host) を設定してください}"
MACMINI_INBOX="${MACMINI_INBOX:-hermes-agent/data/repos/moorestech_logs/harness/bug-report/inbox}"
RSYNC_CMD="${RSYNC_CMD:-rsync}"
SSH_CMD="${SSH_CMD:-ssh -o BatchMode=yes -o ConnectTimeout=5}"
GIT_CMD="${GIT_CMD:-git}"
REPO_ROOT="${MOORESTECH_REPO:-$(cd "$(dirname "$0")/../.." && pwd)}"
MASTER_ROOT="${MOORESTECH_MASTER:-$REPO_ROOT/../moorestech_master}"

log() { echo "[ship] $*" >&2; }

# 未pushコミットを bundle にする。origin に届いていれば何もしない
# Bundle unpushed commits; no-op when origin already has the commit
attach_bundle() {
  local repo="$1" commit="$2" out="$3" id="$4"
  [ -z "$commit" ] && return 0
  [ -d "$repo" ] || { log "repo が無い: $repo"; return 0; }
  if $GIT_CMD -C "$repo" merge-base --is-ancestor "$commit" origin/master 2>/dev/null; then return 0; fi
  if $GIT_CMD -C "$repo" cat-file -e "$commit^{commit}" 2>/dev/null; then
    # bundle の端点は ref でなければならないので一時 ref を切って作り、受け側は refs/bugreport/* で fetch する
    # Bundle endpoints must be refs, so create a temporary ref; the receiver fetches refs/bugreport/*
    $GIT_CMD -C "$repo" update-ref "refs/bugreport/$id" "$commit"
    $GIT_CMD -C "$repo" bundle create "$out" "origin/master..refs/bugreport/$id" && log "bundle: $out"
    $GIT_CMD -C "$repo" update-ref -d "refs/bugreport/$id"
  else
    log "commit が見つからない: $commit ($repo)"
  fi
}

shopt -s nullglob
for box in "$OUTBOX_DIR"/*/; do
  box="${box%/}"; id="$(basename "$box")"
  [ -f "$box/READY" ] || continue
  [ -f "$box/SHIPPED" ] && continue

  commit="$(python3 -c "import json,sys;print(json.load(open(sys.argv[1]))['repository']['commit'])" "$box/manifest.json" 2>/dev/null || echo "")"
  master_commit="$(python3 -c "import json,sys;print(json.load(open(sys.argv[1]))['masterData']['commit'])" "$box/manifest.json" 2>/dev/null || echo "")"
  mkdir -p "$box/repo"
  attach_bundle "$REPO_ROOT" "$commit" "$box/repo/commits.bundle" "$id"
  attach_bundle "$MASTER_ROOT" "$master_commit" "$box/repo/master-commits.bundle" "$id"

  # .partial へ送り切ってから mv でアトミックに公開する（受け側が途中の箱を掴まない）
  # Send into .partial, then publish atomically with mv so the receiver never sees a half box
  if ! $RSYNC_CMD -a --partial "$box/" "$MACMINI_SSH:$MACMINI_INBOX/$id.partial/"; then
    log "rsync 失敗（Tailscale 未接続か）。次回に再試行: $id"; exit 0
  fi
  if ! $SSH_CMD "$MACMINI_SSH" "mv '$MACMINI_INBOX/$id.partial' '$MACMINI_INBOX/$id'"; then
    log "公開 mv 失敗。次回に再試行: $id"; exit 0
  fi
  date -u +%Y-%m-%dT%H:%M:%SZ > "$box/SHIPPED"
  log "shipped: $id"
done
