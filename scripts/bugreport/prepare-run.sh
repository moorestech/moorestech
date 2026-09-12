#!/usr/bin/env bash
# 報告時のコミット＋差分で隔離 worktree を作り、master data worktree・Library・world/ を用意して run.env を書く
# Builds an isolated worktree at the report commit plus diff, the master-data worktree, Library, world/, and run.env
set -euo pipefail
ID="${1:?run id}"
LOGS="${MOORESTECH_LOGS:-$HOME/hermes-agent/data/repos/moorestech_logs}"
REPO="${MOORESTECH_REPO:-$HOME/hermes-agent/data/repos/moorestech}"
WORKTREES="${MOORESTECH_WORKTREES:-$HOME/hermes-agent/data/repos/moorestech-worktrees}"
MASTER="${MOORESTECH_MASTER:-$HOME/hermes-agent/data/repos/moorestech_master}"
MASTER_WORKTREES="${MOORESTECH_MASTER_WORKTREES:-$HOME/hermes-agent/data/repos/moorestech-master-worktrees}"
RUN="$LOGS/runs/$ID"; [ -d "$RUN" ] || RUN="$LOGS/harness/bug-report/runs/$ID"
log() { echo "[prepare] $*" >&2; }

[ -d "$RUN" ] || { log "run ディレクトリが無い: $RUN"; exit 1; }

# manifest は欠損しうる外部入力。読めない項目は空にし理由を全部ログして続行する（添付が欠けても残った資料で調査する裁定）
# The manifest is fallible external input: unreadable fields become empty, every reason is logged, and the run continues
read_manifest() {
  python3 - "$RUN/manifest.json" <<'PY'
import json, shlex, sys

notes = []
data = {}
# 外部JSONのパースは外部境界。壊れた箱で run 全体を落とさないため例外をここで閉じる
# Parsing external JSON is a boundary; the exception is contained here so a broken box cannot kill the run
try:
    with open(sys.argv[1]) as handle:
        data = json.load(handle)
except Exception as error:
    notes.append("manifest.json を読めない（%s: %s）。全項目を空として続行する" % (type(error).__name__, error))
if not isinstance(data, dict):
    notes.append("manifest.json の中身が辞書でない。全項目を空として続行する")
    data = {}

def text(section_name, key):
    section = data.get(section_name)
    if not isinstance(section, dict):
        notes.append("manifest に %s が無い。%s.%s は空として続行する" % (section_name, section_name, key))
        return ""
    value = section.get(key)
    if isinstance(value, str) and value:
        return value
    notes.append("manifest の %s.%s が無い/空。空として続行する" % (section_name, key))
    return ""

# 再現は記録時と同じサーバーデータでしか成立しない。受け側は自分の worktree 配下へ解決するので相対表現を使う
# Reproduction holds only with the recording's own server data; the receiver resolves it under its own worktree, hence the relative form
server = data.get("serverData")
server_relative_to = ""
server_relative_path = ""
server_path = ""
if not isinstance(server, dict):
    notes.append("manifest に serverData が無い。記録時にサーバーが読んだマスタを特定できないため決定性検査は行えない")
else:
    server_relative_to = server.get("relativeTo") if isinstance(server.get("relativeTo"), str) else ""
    server_relative_path = server.get("relativePath") if isinstance(server.get("relativePath"), str) else ""
    server_path = server.get("path") if isinstance(server.get("path"), str) else ""
    if not server_relative_path:
        notes.append("serverData がリポジトリの外を指しているため受け側で解決できない: %s" % (server_path or "(path も空)"))

ticks = data.get("snapshotTicks")
if not isinstance(ticks, list):
    notes.append("manifest の snapshotTicks が無い/配列でない。スナップショット無しで続行する")
    ticks = []
numbers = [tick for tick in ticks if isinstance(tick, int) and not isinstance(tick, bool) and tick > 0]
if len(numbers) != len(ticks):
    notes.append("snapshotTicks に数値でない要素が混ざっている。数値だけを使う: %r" % (ticks,))
if not numbers:
    notes.append("snapshotTicks が空。固定ワールド起動はできないがログ・映像だけで続行する")
latest = str(max(numbers)) if numbers else ""

for item in data.get("missing") or []:
    if isinstance(item, dict):
        notes.append("報告側が欠損を申告している: %s（%s）" % (item.get("item"), item.get("reason")))
    else:
        notes.append("報告側が欠損を申告している: %r" % (item,))

values = [
    ("REPORT_COMMIT", text("repository", "commit")),
    ("REPORT_BRANCH", text("repository", "branch")),
    ("MASTER_COMMIT", text("masterData", "commit")),
    ("SERVER_DATA_RELATIVE_TO", server_relative_to),
    ("SERVER_DATA_RELATIVE_PATH", server_relative_path),
    ("SERVER_DATA_PATH", server_path),
    ("LATEST_TICK", latest),
]
# 値を全部取ってから理由を出す。取得中に増える note を取りこぼさないため
# Resolve every value first, then emit the notes, so notes added while resolving are not lost
for note in notes:
    sys.stderr.write("[prepare] %s\n" % note)
for name, value in values:
    print("%s=%s" % (name, shlex.quote(value)))
PY
}

REPORT_COMMIT=""; REPORT_BRANCH=""; MASTER_COMMIT=""; LATEST_TICK=""
SERVER_DATA_RELATIVE_TO=""; SERVER_DATA_RELATIVE_PATH=""; SERVER_DATA_PATH=""
manifest_env="$(read_manifest)" || log "manifest の読み取りに失敗した。全項目を空として続行する"
eval "$manifest_env"

WORKTREE="$WORKTREES/bugfix-$ID"; COMMIT_MISSING=0; DIFF_APPLY_FAILED=0; MASTER_FAILED=0; MASTER_WORKTREE=""
# 既存の worktree は消さずに失敗させる（他ランの作業物を巻き込まないため）
# Never delete an existing worktree; fail instead so another run's work is not destroyed
[ -e "$WORKTREE" ] && { log "worktree が既に存在する。二重準備を避けて中断: $WORKTREE"; exit 1; }

# 取得できなくても手元の origin/master と bundle で進む（ネットワークは落ちうる外部依存）
# Proceed with the local origin/master and the bundle even when the fetch fails; the network is a fallible dependency
git -C "$REPO" fetch -q origin master || log "origin/master の fetch に失敗した。手元の参照で続行する"
[ -f "$RUN/repo/commits.bundle" ] && git -C "$REPO" fetch -q "$RUN/repo/commits.bundle" '+refs/bugreport/*:refs/bugreport/*' 2>/dev/null || true
if [ -n "$REPORT_COMMIT" ] && git -C "$REPO" cat-file -e "$REPORT_COMMIT^{commit}" 2>/dev/null; then base="$REPORT_COMMIT"; else base="origin/master"; COMMIT_MISSING=1; log "報告コミットが無いため origin/master を土台にする: '$REPORT_COMMIT'"; fi
git -C "$REPO" worktree add -q -b "bugfix/$ID" "$WORKTREE" "$base" || { log "worktree を作れなかった（base=${base}）。この run は準備できない"; exit 1; }
if [ -s "$RUN/repo/head.diff" ]; then
  git -C "$WORKTREE" apply --whitespace=nowarn "$RUN/repo/head.diff" || { DIFF_APPLY_FAILED=1; log "head.diff の適用に失敗"; }
fi
if [ -d "$RUN/repo/untracked" ]; then
  cp -R "$RUN/repo/untracked/." "$WORKTREE/" || log "未追跡ファイルのコピーに失敗した。未追跡分を欠いたまま続行する"
else
  log "未追跡ファイルが箱に無い。未追跡分を欠いたまま続行する"
fi

# master data も報告時の実チェックアウト値で worktree を切る（ピンではなく manifest の値）
# The master-data worktree also uses the manifest's actual checkout, not the pin
MASTER_DIR=""
if [ -n "$MASTER_COMMIT" ] && [ -d "$MASTER" ]; then
  [ -f "$RUN/repo/master-commits.bundle" ] && git -C "$MASTER" fetch -q "$RUN/repo/master-commits.bundle" '+refs/bugreport/*:refs/bugreport/*' 2>/dev/null || true
  if [ -e "$MASTER_WORKTREES/bugfix-$ID" ]; then
    log "master worktree が既に存在するため再利用する: $MASTER_WORKTREES/bugfix-$ID"
  else
    if git -C "$MASTER" worktree add -q --detach "$MASTER_WORKTREES/bugfix-$ID" "$MASTER_COMMIT"; then
      [ -s "$RUN/repo/master.diff" ] && git -C "$MASTER_WORKTREES/bugfix-$ID" apply --whitespace=nowarn "$RUN/repo/master.diff" || true
    else
      log "master data の worktree を作れなかった（commit=${MASTER_COMMIT}）。master data 無しで続行する"
      MASTER_FAILED=1
    fi
  fi
  if [ "$MASTER_FAILED" = "0" ]; then MASTER_WORKTREE="$MASTER_WORKTREES/bugfix-$ID"; MASTER_DIR="$MASTER_WORKTREE/server_v8"; fi
else
  log "master data の worktree を作らない（commit='$MASTER_COMMIT' repo='$MASTER'）"
fi

# 記録時のサーバーデータを、この受け側の worktree 配下へ解決する。MASTER_DIR で代用すると別マスタで再生してしまう
# Resolve the recording's server data under this receiver's worktrees; substituting MASTER_DIR would replay against different masters
SERVER_DATA_DIR=""
case "$SERVER_DATA_RELATIVE_TO" in
  repository) server_data_base="$WORKTREE" ;;
  masterData) server_data_base="$MASTER_WORKTREE" ;;
  *) server_data_base="" ;;
esac
if [ -z "$SERVER_DATA_RELATIVE_PATH" ]; then
  log "記録時のサーバーデータが manifest から分からない（relativeTo='$SERVER_DATA_RELATIVE_TO' path='$SERVER_DATA_PATH'）。決定性検査は行えない"
elif [ -z "$server_data_base" ]; then
  log "記録時のサーバーデータの基準 worktree が無い（relativeTo='$SERVER_DATA_RELATIVE_TO'）。決定性検査は行えない"
elif [ -d "$server_data_base/$SERVER_DATA_RELATIVE_PATH/mods" ]; then
  SERVER_DATA_DIR="$server_data_base/$SERVER_DATA_RELATIVE_PATH"
else
  log "記録時のサーバーデータが受け側に無い（mods/ が無い）: ${server_data_base}/${SERVER_DATA_RELATIVE_PATH}（記録時: ${SERVER_DATA_PATH}）。決定性検査は行えない"
fi

# Library は APFS クローン（AGENTS.md）。無ければ初回インポートに任せる
# Library via APFS clone (AGENTS.md); fall back to a first import when absent
if [ -d "$REPO/moorestech_client/Library" ] && [ ! -d "$WORKTREE/moorestech_client/Library" ]; then
  mkdir -p "$WORKTREE/moorestech_client"
  cp -Rc "$REPO/moorestech_client/Library" "$WORKTREE/moorestech_client/Library" 2>/dev/null \
    || { log "APFS クローンに失敗したため通常コピーにする"; cp -R "$REPO/moorestech_client/Library" "$WORKTREE/moorestech_client/Library" || log "Library のコピーに失敗した。初回インポートに任せて続行する"; }
fi

# world/: 最新スナップショットを save.json にして固定ワールド起動できる形にする
# world/: place the latest snapshot as save.json so a fixed-world boot can load it
WORLD_DIR="$RUN/world"; mkdir -p "$WORLD_DIR"
if [ -z "$LATEST_TICK" ]; then
  log "スナップショットの tick が無いため save.json を置けない。固定ワールド起動はできない"
elif [ -f "$RUN/snapshots/tick_$LATEST_TICK.json" ]; then
  cp "$RUN/snapshots/tick_$LATEST_TICK.json" "$WORLD_DIR/save.json" || log "save.json のコピーに失敗した。固定ワールド起動はできない"
else
  log "tick に対応するスナップショットファイルが無いため save.json を置けない: $RUN/snapshots/tick_$LATEST_TICK.json"
fi
# world.json/map.json はワールド定義。欠けていると固定ワールド起動が別の地形になるので必ず告げる
# world.json/map.json are the world definition; without them a fixed-world boot lands on different terrain
for world_file in world.json map.json; do
  [ -f "$WORLD_DIR/$world_file" ] || log "ワールド定義が箱に無い: $world_file"
done

# 値は %q で書く。manifest 由来の文字列がそのまま入ると run.env を source した側が壊れる
# Values go through %q; a raw manifest string would otherwise break whoever sources run.env
{
  printf 'WORKTREE=%q\n' "$WORKTREE"
  printf 'MASTER_DIR=%q\n' "$MASTER_DIR"
  printf 'SERVER_DATA_DIR=%q\n' "$SERVER_DATA_DIR"
  printf 'WORLD_DIR=%q\n' "$WORLD_DIR"
  printf 'REPORT_COMMIT=%q\n' "$REPORT_COMMIT"
  printf 'REPORT_BRANCH=%q\n' "$REPORT_BRANCH"
  printf 'LATEST_TICK=%q\n' "$LATEST_TICK"
  printf 'COMMIT_MISSING=%q\n' "$COMMIT_MISSING"
  printf 'DIFF_APPLY_FAILED=%q\n' "$DIFF_APPLY_FAILED"
  printf 'MASTER_FAILED=%q\n' "$MASTER_FAILED"
} > "$RUN/run.env"
log "prepared: $RUN/run.env"
