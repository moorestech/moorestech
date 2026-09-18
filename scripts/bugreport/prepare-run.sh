#!/usr/bin/env bash
# 報告時のコミット＋差分で隔離 worktree を作り、master data worktree・Library・world/ を用意して run.env を書く
# Builds an isolated worktree at the report commit plus diff, the master-data worktree, Library, world/, and run.env
set -euo pipefail
ID="${1:?run id}"
HERE="$(cd "$(dirname "$0")" && pwd)"
# 既定値はスクリプト自身の位置から導出する（inbox-poller.sh と同じ。supervisor は HOME を差し替えるため $HOME 基準は不可）
# Defaults derive from the script's own location (same as inbox-poller.sh; supervisor swaps HOME, so $HOME-based defaults break)
REPO="${MOORESTECH_REPO:-$(cd "$HERE/../.." && pwd)}"
LOGS="${MOORESTECH_LOGS:-$REPO/../moorestech_logs}"
WORKTREES="${MOORESTECH_WORKTREES:-$REPO/../moorestech-worktrees}"
MASTER="${MOORESTECH_MASTER:-$REPO/../moorestech_master}"
MASTER_WORKTREES="${MOORESTECH_MASTER_WORKTREES:-$REPO/../moorestech-master-worktrees}"
RUN="$LOGS/runs/$ID"; [ -d "$RUN" ] || RUN="$LOGS/harness/bug-report/runs/$ID"
log() { echo "[prepare] $*" >&2; }

[ -d "$RUN" ] || { log "run ディレクトリが無い: $RUN"; exit 1; }

# manifest は欠損しうる外部入力。読めない項目は空にし理由を全部ログして続行する（添付が欠けても残った資料で調査する裁定）
# The manifest is fallible external input: unreadable fields become empty, every reason is logged, and the run continues
read_manifest() {
  python3 "$HERE/read-manifest.py" "$RUN/manifest.json"
}

REPORT_COMMIT=""; REPORT_BRANCH=""; MASTER_COMMIT=""; LATEST_TICK=""; WORLD_DEFINITION=""
SERVER_DATA_RELATIVE_TO=""; SERVER_DATA_RELATIVE_PATH=""; SERVER_DATA_PATH=""
manifest_env="$(read_manifest)" || log "manifest の読み取りに失敗した。全項目を空として続行する"
eval "$manifest_env"

# 復元の縮退は全部フラグにする。受け側のエージェントは run.env のこの集合だけを見れば再現環境の欠けが分かる
# Every restoration shortfall becomes a flag here, so the agent only has to read this one set in run.env
WORKTREE="$WORKTREES/bugfix-$ID"; COMMIT_MISSING=0; DIFF_APPLY_FAILED=0; DIFF_ABSENT=0; UNTRACKED_FAILED=0
MASTER_FAILED=0; MASTER_DIFF_APPLY_FAILED=0; MASTER_DIFF_ABSENT=0; MASTER_UNTRACKED_FAILED=0; MASTER_WORKTREE=""
# 既存の worktree は消さずに失敗させる（他ランの作業物を巻き込まないため）
# Never delete an existing worktree; fail instead so another run's work is not destroyed
[ -e "$WORKTREE" ] && { log "worktree が既に存在する。二重準備を避けて中断: $WORKTREE"; exit 1; }

# 取得できなくても手元の origin/master と bundle で進む（ネットワークは落ちうる外部依存）
# Proceed with the local origin/master and the bundle even when the fetch fails; the network is a fallible dependency
git -C "$REPO" fetch -q origin master || log "origin/master の fetch に失敗した。手元の参照で続行する"
if [ -f "$RUN/repo/commits.bundle" ]; then
  git -C "$REPO" fetch -q "$RUN/repo/commits.bundle" '+refs/bugreport/*:refs/bugreport/*' \
    || log "commits.bundle を取り込めなかった。報告者の未pushコミットは使えない: $RUN/repo/commits.bundle"
else
  log "commits.bundle が箱に無い。報告コミットは手元の参照にある場合だけ使える"
fi
if [ -n "$REPORT_COMMIT" ] && git -C "$REPO" cat-file -e "$REPORT_COMMIT^{commit}" 2>/dev/null; then base="$REPORT_COMMIT"; else base="origin/master"; COMMIT_MISSING=1; log "報告コミットが無いため origin/master を土台にする: '$REPORT_COMMIT'"; fi
git -C "$REPO" worktree add -q -b "bugfix/$ID" "$WORKTREE" "$base" || { log "worktree を作れなかった（base=${base}）。この run は準備できない"; exit 1; }
if [ -s "$RUN/repo/head.diff" ]; then
  git -C "$WORKTREE" apply --whitespace=nowarn "$RUN/repo/head.diff" || { DIFF_APPLY_FAILED=1; log "head.diff の適用に失敗"; }
else
  DIFF_ABSENT=1; log "head.diff が無い/空。報告時の未コミット差分を当てられない: $RUN/repo/head.diff"
fi
if [ -d "$RUN/repo/untracked" ]; then
  cp -R "$RUN/repo/untracked/." "$WORKTREE/" || { UNTRACKED_FAILED=1; log "未追跡ファイルのコピーに失敗した。未追跡分を欠いたまま続行する"; }
else
  log "未追跡ファイルが箱に無い。未追跡分を欠いたまま続行する"
fi

# master data も報告時の実チェックアウト値で worktree を切る（ピンではなく manifest の値）
# The master-data worktree also uses the manifest's actual checkout, not the pin
MASTER_DIR=""
# master 側の復元も本体側と同じ扱いにする。差分・未追跡を当て、落ちたらフラグとログを必ず残す
# The master side restores exactly like the code side: apply the diff and untracked files, flag and log every failure
restore_master_worktree() {
  local dir="$1"
  if [ -s "$RUN/repo/master.diff" ]; then
    git -C "$dir" apply --whitespace=nowarn "$RUN/repo/master.diff" \
      || { MASTER_DIFF_APPLY_FAILED=1; log "master.diff の適用に失敗。マスタデータが報告時と違う状態で再現する"; }
  else
    MASTER_DIFF_ABSENT=1; log "master.diff が無い/空。マスタの未コミット差分を当てられない: $RUN/repo/master.diff"
  fi
  if [ -d "$RUN/repo/master-untracked" ]; then
    cp -R "$RUN/repo/master-untracked/." "$dir/" \
      || { MASTER_UNTRACKED_FAILED=1; log "マスタの未追跡ファイルのコピーに失敗した。未追跡分を欠いたまま続行する: $dir"; }
  else
    log "マスタの未追跡ファイルが箱に無い。未追跡分を欠いたまま続行する"
  fi
}
if [ -n "$MASTER_COMMIT" ] && [ -d "$MASTER" ]; then
  if [ -f "$RUN/repo/master-commits.bundle" ]; then
    git -C "$MASTER" fetch -q "$RUN/repo/master-commits.bundle" '+refs/bugreport/*:refs/bugreport/*' \
      || log "master-commits.bundle を取り込めなかった。マスタの未pushコミットは使えない: $RUN/repo/master-commits.bundle"
  else
    log "master-commits.bundle が箱に無い。マスタの報告コミットは手元の参照にある場合だけ使える"
  fi
  if [ -e "$MASTER_WORKTREES/bugfix-$ID" ]; then
    log "master worktree が既に存在するため再利用する: $MASTER_WORKTREES/bugfix-$ID"
  else
    if git -C "$MASTER" worktree add -q --detach "$MASTER_WORKTREES/bugfix-$ID" "$MASTER_COMMIT"; then
      restore_master_worktree "$MASTER_WORKTREES/bugfix-$ID"
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

# 土台の判定は resolver（materialize-world.cs）1箇所に寄せる。ここで箱の world/ をそのまま使うのは「full の宣言があり map.json もある」箱だけで、生成ワールド・旧版・読めない宣言は world-materialized/ に save.json だけ置いて実体化を予約する（ADR 0064・D10）
# The base is decided by the resolver (materialize-world.cs) alone; only a box declaring full with map.json present uses world/ as is, and generated, old or unreadable declarations get just save.json in world-materialized/ with materialization reserved (ADR 0064, D10)
WORLD_DIR="$RUN/world"; WORLD_MATERIALIZE_PENDING=0; WORLD_NOT_CAPTURED=0
if [ "$WORLD_DEFINITION" = "not-captured" ]; then
  # world.json の無い world/ で起動すると EnsureWorld が落ち、run-scenario.sh は300秒空転する。save.json を置かず観察の関門で止める
  # Booting a world/ without world.json fails in EnsureWorld and run-scenario.sh idles 300s; withhold save.json so the observation gate stops it
  WORLD_NOT_CAPTURED=1; log "報告側が記録時のワールドを取り込めなかった箱（worldDefinition=not-captured）。固定ワールド起動はできないので save.json を置かず観察を飛ばさせる"
elif [ "$WORLD_DEFINITION" != "full" ] || [ ! -f "$RUN/world/map.json" ]; then
  WORLD_DIR="$RUN/world-materialized"; WORLD_MATERIALIZE_PENDING=1
  case "$WORLD_DEFINITION" in
    generated-world-json-only) log "生成ワールドの箱は地形を同梱しない。観察の前に materialize-world.cs で地形付きワールドを実体化する: $WORLD_DIR" ;;
    *) log "worldDefinition='$WORLD_DEFINITION' の箱は自分では土台を決めず、観察の前に materialize-world.cs（resolver）の判定で実体化する: $WORLD_DIR" ;;
  esac
fi
# 最新スナップショットを save.json にして固定ワールド起動できる形にする
# Place the latest snapshot as save.json so a fixed-world boot can load it
if [ "$WORLD_NOT_CAPTURED" = "1" ]; then
  :
elif [ -z "$LATEST_TICK" ]; then
  log "スナップショットの tick が無いため save.json を置けない。固定ワールド起動はできない"
elif [ -f "$RUN/snapshots/tick_$LATEST_TICK.json" ]; then
  mkdir -p "$WORLD_DIR"
  cp "$RUN/snapshots/tick_$LATEST_TICK.json" "$WORLD_DIR/save.json" || log "save.json のコピーに失敗した。固定ワールド起動はできない"
else
  log "tick に対応するスナップショットファイルが無いため save.json を置けない: $RUN/snapshots/tick_$LATEST_TICK.json"
fi
# world.json/map.json はワールド定義。欠けていると固定ワールド起動が別の地形になるので必ず告げる（生成ワールドの箱は map.json を持たないのが正）
# world.json/map.json are the world definition; without them a fixed-world boot lands on different terrain (a generated-world box rightly has no map.json)
case "$WORLD_DEFINITION" in
  not-captured) world_files="" ;;
  generated-world-json-only) world_files="world.json" ;;
  *) world_files="world.json map.json" ;;
esac
for world_file in $world_files; do
  [ -f "$RUN/world/$world_file" ] || log "ワールド定義が箱に無い: $world_file"
done
# 観察の起動引数は箱の world.json から取る。実体化した土台も同じ worldId から引き当てるので seed・mapMode は変わらない
# The observation boot arguments come from the box's world.json; the materialized base is located by the same worldId, so seed and mapMode match
WORLD_MAP_MODE=""; WORLD_SEED=""; world_meta_env=""
[ "$WORLD_NOT_CAPTURED" = "1" ] || world_meta_env="$(python3 "$HERE/read-world-meta.py" "$RUN/world/world.json")" || log "world.json の読み取りに失敗した。mapMode・seed を空として続行する"
eval "$world_meta_env"

# 値は %q で書く。manifest 由来の文字列がそのまま入ると run.env を source した側が壊れる
# Values go through %q; a raw manifest string would otherwise break whoever sources run.env
{
  printf 'WORKTREE=%q\n' "$WORKTREE"
  printf 'MASTER_DIR=%q\n' "$MASTER_DIR"
  printf 'SERVER_DATA_DIR=%q\n' "$SERVER_DATA_DIR"
  printf 'WORLD_DIR=%q\n' "$WORLD_DIR"
  printf 'WORLD_MATERIALIZE_PENDING=%q\n' "$WORLD_MATERIALIZE_PENDING"
  printf 'WORLD_NOT_CAPTURED=%q\n' "$WORLD_NOT_CAPTURED"
  printf 'WORLD_MAP_MODE=%q\n' "$WORLD_MAP_MODE"
  printf 'WORLD_SEED=%q\n' "$WORLD_SEED"
  printf 'REPORT_COMMIT=%q\n' "$REPORT_COMMIT"
  printf 'REPORT_BRANCH=%q\n' "$REPORT_BRANCH"
  printf 'LATEST_TICK=%q\n' "$LATEST_TICK"
  printf 'COMMIT_MISSING=%q\n' "$COMMIT_MISSING"
  printf 'DIFF_APPLY_FAILED=%q\n' "$DIFF_APPLY_FAILED"
  printf 'DIFF_ABSENT=%q\n' "$DIFF_ABSENT"
  printf 'UNTRACKED_FAILED=%q\n' "$UNTRACKED_FAILED"
  printf 'MASTER_FAILED=%q\n' "$MASTER_FAILED"
  printf 'MASTER_DIFF_APPLY_FAILED=%q\n' "$MASTER_DIFF_APPLY_FAILED"
  printf 'MASTER_DIFF_ABSENT=%q\n' "$MASTER_DIFF_ABSENT"
  printf 'MASTER_UNTRACKED_FAILED=%q\n' "$MASTER_UNTRACKED_FAILED"
} > "$RUN/run.env"
log "prepared: $RUN/run.env"
