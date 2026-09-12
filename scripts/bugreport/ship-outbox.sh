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

# manifest は壊れうる外部入力。読めない理由をログして空を返す（無音で bundle 無しにしない）
# The manifest is fallible external input: log why it could not be read and return empty, never silently
read_manifest_field() {
  local box="$1" section="$2" key="$3" value="" err=""
  err="$(mktemp)"
  value="$(python3 -c 'import json, sys
data = json.load(open(sys.argv[1]))
value = data[sys.argv[2]][sys.argv[3]]
if not isinstance(value, str):
    sys.stderr.write("%s.%s が文字列でない: %r" % (sys.argv[2], sys.argv[3], value))
    value = ""
print(value)' "$box/manifest.json" "$section" "$key" 2>"$err")" || value=""
  if [ -s "$err" ]; then
    log "manifest の ${section}.${key} を読めない（$(tr '\n' ' ' < "$err")）: $box/manifest.json"
  fi
  rm -f "$err"
  printf '%s' "$value"
}

# 未pushコミットを bundle にする。origin に届いていれば何もしない。3状態を箱へ残し受け側が読む
# Bundle unpushed commits (no-op when origin already has them); record the three states in the box for the receiver
attach_bundle() {
  local repo="$1" commit="$2" out="$3" id="$4" label="$5" status_file="$6"
  if [ -z "$commit" ]; then
    log "manifest にコミットが無いため bundle を付けない（報告者のローカルコミットは受け側へ届かない）: $label"
    echo "$label: skipped-no-commit" >> "$status_file"; return 0
  fi
  if [ ! -d "$repo" ]; then
    log "repo が無い: $repo"
    echo "$label: skipped-no-repo ($repo)" >> "$status_file"; return 0
  fi
  if $GIT_CMD -C "$repo" merge-base --is-ancestor "$commit" origin/master 2>/dev/null; then
    echo "$label: not-needed-origin-has-commit" >> "$status_file"; return 0
  fi
  if ! $GIT_CMD -C "$repo" cat-file -e "$commit^{commit}" 2>/dev/null; then
    log "commit が見つからない: $commit ($repo)"
    echo "$label: failed-commit-not-found ($commit)" >> "$status_file"; return 0
  fi
  # bundle の端点は ref でなければならないので一時 ref を切って作り、受け側は refs/bugreport/* で fetch する
  # Bundle endpoints must be refs, so create a temporary ref; the receiver fetches refs/bugreport/*
  $GIT_CMD -C "$repo" update-ref "refs/bugreport/$id" "$commit"
  if $GIT_CMD -C "$repo" bundle create "$out" "origin/master..refs/bugreport/$id"; then
    log "bundle: $out"
    echo "$label: created" >> "$status_file"
  else
    rm -f "$out"
    log "bundle 作成に失敗した（受け側は報告者のローカルコミットを持てない）: $out"
    echo "$label: failed-bundle-create" >> "$status_file"
  fi
  $GIT_CMD -C "$repo" update-ref -d "refs/bugreport/$id"
}

# 到達性は箱ごとではなく最初に1回だけ見る。届かないときだけ全体を降りる（1箱の障害で全報告を止めない）
# Probe reachability once up front; only an unreachable host aborts the whole run, never a single bad box
if ! $SSH_CMD "$MACMINI_SSH" true; then
  log "Mac mini へ到達できない（Tailscale 未接続か）。今回は何も運ばない: $MACMINI_SSH"
  exit 0
fi

blocked=0
shopt -s nullglob
for box in "$OUTBOX_DIR"/*/; do
  box="${box%/}"; id="$(basename "$box")"
  [ -f "$box/READY" ] || continue
  [ -f "$box/SHIPPED" ] && continue
  # 手動確認が要る箱は理由が箱に書いてある。毎回同じ全文を吐かず、件数だけ最後に出す
  # Boxes needing manual attention carry their reason; report only the count at the end instead of repeating it
  if [ -f "$box/SHIP_BLOCKED" ]; then blocked=$((blocked + 1)); continue; fi

  commit="$(read_manifest_field "$box" repository commit)"
  master_commit="$(read_manifest_field "$box" masterData commit)"
  mkdir -p "$box/repo"
  : > "$box/repo/bundle-status.txt"
  attach_bundle "$REPO_ROOT" "$commit" "$box/repo/commits.bundle" "$id" "commits.bundle" "$box/repo/bundle-status.txt"
  attach_bundle "$MASTER_ROOT" "$master_commit" "$box/repo/master-commits.bundle" "$id" "master-commits.bundle" "$box/repo/bundle-status.txt"

  # .partial へ送り切ってから mv でアトミックに公開する（受け側が途中の箱を掴まない）
  # Send into .partial, then publish atomically with mv so the receiver never sees a half box
  # rsync 自身の ssh にも BatchMode/ConnectTimeout を効かせる（未接続時に無応答で固まらせない）
  # Pass the same ssh options to rsync's own transport so an unreachable host fails fast
  if ! $RSYNC_CMD -a --partial -e "$SSH_CMD" "$box/" "$MACMINI_SSH:$MACMINI_INBOX/$id.partial/"; then
    log "rsync 失敗。この箱は次回に再試行し、後続の箱は続けて運ぶ: $id"; continue
  fi
  # 宛先が既にあると mv は入れ子にしてしまうため、リモートで存在を検査して失敗（3）にする
  # A pre-existing destination would make mv nest the box, so check remotely and fail with 3
  mv_rc=0
  $SSH_CMD "$MACMINI_SSH" "[ -e '$MACMINI_INBOX/$id' ] && exit 3; mv '$MACMINI_INBOX/$id.partial' '$MACMINI_INBOX/$id'" || mv_rc=$?
  if [ "$mv_rc" -eq 3 ]; then
    log "公開先が既に存在する（前回の mv が成功して SHIPPED 前に落ちた疑い）。入れ子破損を避けるため送らない。手動確認が必要: $MACMINI_INBOX/$id"
    echo "公開先 $MACMINI_INBOX/$id が既に存在するため運搬を止めた。受け側を確認して、不要ならこの箱ごと削除する" > "$box/SHIP_BLOCKED"
    blocked=$((blocked + 1)); continue
  fi
  if [ "$mv_rc" -ne 0 ]; then
    log "公開 mv 失敗（exit ${mv_rc}）。この箱は次回に再試行し、後続の箱は続けて運ぶ: $id"; continue
  fi
  date -u +%Y-%m-%dT%H:%M:%SZ > "$box/SHIPPED"
  log "shipped: $id"
done

if [ "$blocked" -gt 0 ]; then
  log "手動確認待ちの箱が ${blocked} 件ある（各箱の SHIP_BLOCKED に理由がある）: $OUTBOX_DIR"
fi
