# バグ報告 C: 運搬スクリプト・Mac mini inbox・自動修正ラン Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** MacBookのoutboxに書かれたバンドルをTailscale経由でMac miniのinboxへ運び、Mac miniが1件ずつ隔離worktreeを切って「決定性検査→再現→修正→合成NUnit→draft PR」の自動修正ランを回し、結果を `fix-result.json` と bd に残す（ADR 0057 の運搬と再現側。初版完成条件「Editorで報告→Mac mini再現→draft PR」の通し）。

**Architecture:** (1) 運搬は `scripts/bugreport/ship-outbox.sh`（launchd 60秒周期）。`READY` のある箱だけを対象にし、未pushコミットを `git bundle` で添付してから `rsync` で `inbox/<id>.partial/` へ送り、完了後に `mv` でアトミックに `inbox/<id>/` にする。届かない機材では何もしない（裁定）。(2) Mac mini側は `scripts/bugreport/inbox-poller.sh`（always-on supervisor の periodic に登録。単一飛行ロック）が inbox から1件取り `moorestech_logs/harness/bug-report/runs/<id>/` へ移し、`prepare-run.sh` で報告時コミット＋差分のworktreeとmaster dataのworktreeを用意し、`claude -p "/bug-report-auto-fix <id>"` を起動する。(3) スキル `bug-report-auto-fix` は無人関所（`unattended-gate.py`）付きで、決定性検査（`SnapshotReplayer` を全連続ペアに適用）→スナップショットからのプレイテスト起動と観察→`debug-workflow`→修正→合成NUnit→masterでも落ちるかで修正先を決める→`moores-code-review`→`pr-create`＋draft化→`fix-result.json`。再現不能・仕様曖昧はコードを触らず bd に積む。

**Tech Stack:** bash（rsync・ssh・git bundle・launchd）、Python 3（テスト）、Claude Code スキル（frontmatter hooks）、Unity C#（`PacketLogJsonDumper`）、プレイテストDSL、`gh` CLI、bd。

## Requirements

- R1. 運搬: `ship-outbox.sh` が `<GameSystemDirectory>/BugReports/outbox/*/READY` を持ち `SHIPPED` を持たない箱を古い順に送る。送る前に `manifest.repository.commit` が `origin` に無ければ `git bundle create repo/commits.bundle origin/master..<commit>`、`masterData.commit` も同様に `repo/master-commits.bundle` を作る。`rsync -a --partial` で `<inbox>/<id>.partial/` へ、成功後 `ssh mv` で `<inbox>/<id>/` へ。成功で箱に `SHIPPED` を書く。ssh/rsync が失敗したら箱はそのまま残しログを出して終了（次回再試行）。受入: コマンド差し替え（`SSH_CMD`/`RSYNC_CMD`）のテストでローカル一時ディレクトリへ「送信」され、`SHIPPED` が付き、2回目は送られない。
- R2. launchd: `scripts/bugreport/launchd/com.moorestech.bugreport-shipper.plist` を `~/Library/LaunchAgents/` に置く手順と `StartInterval=60`。設定は `~/.config/moorestech/bugreport-shipper.env`（`MACMINI_SSH`・`MACMINI_INBOX`）。受入: `launchctl list | grep bugreport-shipper` で登録される。
- R3. inbox poller: `inbox-poller.sh` が `inbox/*/READY` を古い順に1件、`mkdir` ロックで単一飛行、`runs/<id>/` へ移動、`prepare-run.sh <id>` → `claude -p` 起動（`--permission-mode bypassPermissions --output-format json`、stdout を `runs/<id>/claude.out.json`）、終了後 `fix-result.json` の有無を確認し、無ければ `{"status":"failure","summary":"claude exited without fix-result.json"}` を書く。最後に logs repo を `git add runs/<id>`（`video.mp4`・`frames/` は `.gitignore`）→ commit → push。受入: `CLAUDE_CMD` を差し替えたテストでラン1件が run ディレクトリ・result・commit を残す。
- R4. prepare-run: `prepare-run.sh <id>` が (a) `git fetch origin master`、(b) bundle があれば `git fetch <bundle>`、(c) `git worktree add -b bugfix/<id> <worktrees>/bugfix-<id> <commit>`（commit が無ければ `origin/master` にフォールバックし `run.env` に `COMMIT_MISSING=1`）、(d) `git apply repo/head.diff`（失敗は `run.env` に `DIFF_APPLY_FAILED=1`）と `repo/untracked/` のコピー、(e) master data worktree `<master-worktrees>/bugfix-<id>` を `masterData.commit` で作り `master.diff` を適用、(f) `moorestech_client/Library` を `cp -Rc`、(g) `world/` ディレクトリ（`world.json`・`map.json`・最新 `tick_*.json` を `save.json` として）を `runs/<id>/world/` に組む、(h) `runs/<id>/run.env` に `WORKTREE`・`MASTER_DIR`・`WORLD_DIR`・`REPORT_COMMIT`・`LATEST_TICK` を書く。受入: 一時 bare repo からのテストで worktree・ブランチ・diff適用・world/ が正しく作られる。
- R5. パケットログの可読化: `Server.Boot/Replay/PacketLogJsonDumper.cs` が `packets_*.bin` を読み `MessagePackSerializer.ConvertToJson` で `{"tick":..,"tag":"va:...","json":{...}}` の JSON Lines を書く。EDCスニペット `dump-packets.cs` がバンドルの全区間を `packets.jsonl` に出す。受入: CombinedTest で PlaceBlock を含むログを dump すると `"tag":"va:placeBlock"`（実タグ名は `PlaceBlockProtocol.ProtocolTag` で確認）を含む。
- R6. 決定性検査スニペット `replay-check.cs`: バンドルの `snapshotTicks` の連続ペア全てで `SnapshotReplayer.Replay`→`SnapshotJsonComparer.Compare` を行い `replay-check.json`（`pairs[]{from,to,equal,differences[]}`・`allEqual`）を書く。受入: plan A の決定性テストで作ったスナップショット群に対して `allEqual=true`。
- R7. 観察シナリオ `bug-report-observe.cs`: `PLAYTEST_WORLD_DIRECTORY`（R4 の world/）で起動し、開幕スキットがあれば飛ばし、報告のカメラ位置へワープ（manifest の `clientState.playerPosition`）、30秒録画（`Record = true`）とスクショ3枚、Unityエラーログを `result.json` に残す。受入: 実バンドルで `Success: true`・録画0byteでない。
- R8. スキル `.agents/skills/bug-report-auto-fix/SKILL.md`: frontmatter hooks で `unattended-gate.py ask` / `stop bugfix`。手順は Step 1〜9（読む→決定性検査→観察→debug-workflow→修正→合成NUnit→修正先判定→レビュー→PR→result）。終端は `fix-result.json`（`{"status":"fixed|not_reproduced|needs_ruling|failure","pr_number":..,"base":"master|<branch>","determinism":"ok|diverged","summary":"...","remaining":"..."}`）。上限なし（裁定）。受入: スキルの手順が `daily-build-repair` と同じ「結果ファイルで終える」契約を持つ。
- R9. 無人関所: `unattended-gate.py` に `bugfix` ジョブを追加（`BUGFIX_PROMPT_RE = r"/bug-report-auto-fix\s+([A-Za-z0-9_-]+)"`、run dir `BUG_REPORT_RUNDIR_BASE/<id>`、goal `fix-result.json`）。受入: `scripts/tests/test_unattended_gate_bugfix.py` で「result 無しの stop は block、有りは通す」。
- R10. logs repo: `../moorestech_logs/harness/bug-report/{inbox,runs}/` と `.gitignore`（`harness/bug-report/inbox/`、`harness/bug-report/runs/*/video.mp4`、`harness/bug-report/runs/*/frames/`、`harness/bug-report/runs/*/repo/untracked/`、`harness/bug-report/runs/*/repo/*.bundle`）を追加し、ブランチ→push→PR。受入: PR が存在する。
- R11. 通し（初版完成条件）: Mac mini で supervisor に poller を登録し、MacBook から実報告を1件送って draft PR まで到達する。受入: `fix-result.json` の `status` が `fixed` か `not_reproduced` か `needs_ruling` のいずれかで、PR（fixed の場合）が draft で存在する。結果を判断記録へ書く。
- やらないこと: HTTP受け口／Tailscale不可時の手当て（裁定: 何もしない）／並列ラン（サーバーポート11564固定）／自動マージ／時間・利用枠の上限／クラッシュ報告／Windows機からの運搬。

## Global Constraints

- 作業ブランチ: `feature/bug-report-auto-fix`（plan A・B 完了コミット以降）。`../moorestech_logs` の変更は同名ブランチで push して PR を作る（AGENTS.md「別リポジトリも push して PR」）。
- Mac mini 固有の実行環境（`/Users/sakastudio/hermes-agent/data/repos/…`・supervisor・`moores-wt`）は本repoに無い。スクリプトは**パスを環境変数で受け、既定値を Mac mini の実配置にする**（`MOORESTECH_REPO`＝`~/hermes-agent/data/repos/moorestech`、`MOORESTECH_LOGS`＝`~/hermes-agent/data/repos/moorestech_logs`、`MOORESTECH_WORKTREES`＝`~/hermes-agent/data/repos/moorestech-worktrees`、`MOORESTECH_MASTER`＝`~/hermes-agent/data/repos/moorestech_master`、`MOORESTECH_MASTER_WORKTREES`＝`~/hermes-agent/data/repos/moorestech-master-worktrees`）。MacBook でのテストは全て一時ディレクトリへ差し替えて行う。
- シェルは `#!/usr/bin/env bash` + `set -euo pipefail`。外部コマンドは変数（`SSH_CMD`・`RSYNC_CMD`・`CLAUDE_CMD`・`GIT_CMD`）で差し替え可能にしテストで置き換える。無音の失敗禁止（`echo "[ship] ..." >&2`）。
- `claude -p` は非対話。ADR 0023 の「cmux 上の対話モード」ではなく `-p` を使う理由は判断記録に書く（poller が transcript 監視を持たないため）。
- Unity は worktree ごとに `uloop launch <worktree>/moorestech_client` で起動し、終了時に `uloop launch --quit` する。サーバーポート11564固定のためランは直列（poller のロック）。
- Mac mini の `dev-server-reaper`（coding-agent に所有されない GUI Editor を30分で kill）に当たる可能性がある。R11 で「1ランが30分を超えても Editor が生存するか」を確認し、殺される場合は Mac mini側 reaper の除外条件（`-batchmode` 引数）を判断記録に記録して次の裁定に回す。
- コメント規約（日英2行）はシェル・Python にも適用。1ファイル200行以下。
- 各タスク末尾でコミット。コミットメッセージ末尾に以下を付ける:
  ```
  Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01SE4dG7rpvBJhbf1rsbiQXN
  ```

---

### Task 1: 運搬スクリプト `ship-outbox.sh` と launchd

**Files:**
- Create: `scripts/bugreport/ship-outbox.sh`
- Create: `scripts/bugreport/launchd/com.moorestech.bugreport-shipper.plist`
- Create: `scripts/bugreport/README.md`（MacBook側の導入手順・env ファイル）
- Test: `scripts/bugreport/tests/test-ship-outbox.sh`

**Interfaces:**
- Consumes: plan B の outbox レイアウト（`<box>/READY`・`manifest.json` の `repository.commit`・`masterData.commit`）
- Produces: 箱に `SHIPPED`、Mac mini 側 `<MACMINI_INBOX>/<id>/`（`READY` 付き）、`repo/commits.bundle`・`repo/master-commits.bundle`（必要時）

- [ ] **Step 1: テストを書く**

`scripts/bugreport/tests/test-ship-outbox.sh`:
```bash
#!/usr/bin/env bash
# ship-outbox.sh を ssh/rsync 差し替えで検証する。ローカルの一時 inbox へ「送信」される
# Verifies ship-outbox.sh with ssh/rsync stubs; boxes are "shipped" into a local temp inbox
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

OUTBOX="$TMP/outbox"; INBOX="$TMP/inbox"; mkdir -p "$OUTBOX/20260911_120000_aaaa1111" "$INBOX"
cat > "$OUTBOX/20260911_120000_aaaa1111/manifest.json" <<JSON
{"repository":{"commit":"0000000000000000000000000000000000000000","dirty":false},"masterData":{"commit":"","dirty":false}}
JSON
echo hello > "$OUTBOX/20260911_120000_aaaa1111/screenshot.png"
touch "$OUTBOX/20260911_120000_aaaa1111/READY"
mkdir -p "$OUTBOX/20260911_110000_notready"   # READY が無い箱は送らない

# rsync は cp -R、ssh は引数のコマンドをローカルで実行するスタブ
# rsync stub copies locally; ssh stub runs the remote command locally
cat > "$TMP/rsync" <<'SH'
#!/usr/bin/env bash
src="${@: -2:1}"; dst="${@: -1}"; dst="${dst#*:}"; mkdir -p "$dst"; cp -R "$src"/. "$dst"/
SH
cat > "$TMP/ssh" <<'SH'
#!/usr/bin/env bash
shift; eval "$@"
SH
chmod +x "$TMP/rsync" "$TMP/ssh"

OUTBOX_DIR="$OUTBOX" MACMINI_SSH="stub@host" MACMINI_INBOX="$INBOX" RSYNC_CMD="$TMP/rsync" SSH_CMD="$TMP/ssh" GIT_CMD=true \
  bash "$HERE/../ship-outbox.sh"

test -f "$INBOX/20260911_120000_aaaa1111/READY" || { echo "NG: READY が inbox に無い"; exit 1; }
test -f "$INBOX/20260911_120000_aaaa1111/screenshot.png" || { echo "NG: ファイルが送られていない"; exit 1; }
test -f "$OUTBOX/20260911_120000_aaaa1111/SHIPPED" || { echo "NG: SHIPPED が付いていない"; exit 1; }
test ! -e "$INBOX/20260911_110000_notready" || { echo "NG: READY 無しの箱が送られた"; exit 1; }

# 2回目は何も送らない（SHIPPED 済み）
# A second run ships nothing (already SHIPPED)
rm -rf "$INBOX/20260911_120000_aaaa1111"
OUTBOX_DIR="$OUTBOX" MACMINI_SSH="stub@host" MACMINI_INBOX="$INBOX" RSYNC_CMD="$TMP/rsync" SSH_CMD="$TMP/ssh" GIT_CMD=true \
  bash "$HERE/../ship-outbox.sh"
test ! -e "$INBOX/20260911_120000_aaaa1111" || { echo "NG: SHIPPED 済みの箱が再送された"; exit 1; }
echo "OK"
```

Run: `bash scripts/bugreport/tests/test-ship-outbox.sh`
Expected: `ship-outbox.sh` が無いので失敗

- [ ] **Step 2: `ship-outbox.sh` を書く**

```bash
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
```
（テストでは `MACMINI_INBOX` を絶対パスで渡す。実運用では Mac mini のホームからの相対パス）

- [ ] **Step 3: launchd plist と README**

`scripts/bugreport/launchd/com.moorestech.bugreport-shipper.plist`:
```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>Label</key><string>com.moorestech.bugreport-shipper</string>
  <key>ProgramArguments</key><array>
    <string>/bin/bash</string>
    <string>/Users/katsumi/moorestech/scripts/bugreport/ship-outbox.sh</string>
  </array>
  <key>StartInterval</key><integer>60</integer>
  <key>StandardOutPath</key><string>/Users/katsumi/Library/Logs/moorestech/bugreport-shipper.log</string>
  <key>StandardErrorPath</key><string>/Users/katsumi/Library/Logs/moorestech/bugreport-shipper.log</string>
  <key>EnvironmentVariables</key><dict>
    <key>PATH</key><string>/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin</string>
  </dict>
</dict></plist>
```

`scripts/bugreport/README.md`（MacBook側の導入）:
```markdown
# バグ報告の運搬（MacBook側）

1. `~/.config/moorestech/bugreport-shipper.env` を作る:
   ```
   MACMINI_SSH=sakastudio@<Mac miniのTailscaleホスト名>
   MACMINI_INBOX=hermes-agent/data/repos/moorestech_logs/harness/bug-report/inbox
   ```
2. `mkdir -p ~/Library/Logs/moorestech && cp scripts/bugreport/launchd/com.moorestech.bugreport-shipper.plist ~/Library/LaunchAgents/ && launchctl load ~/Library/LaunchAgents/com.moorestech.bugreport-shipper.plist`
3. 手動実行: `bash scripts/bugreport/ship-outbox.sh`。ログは `~/Library/Logs/moorestech/bugreport-shipper.log`
4. Tailscale で届かないときは何もしない（裁定 2026-09-11）。箱は outbox に残り、次回接続時に送られる
```

- [ ] **Step 4: テスト・コミット**

Run: `bash scripts/bugreport/tests/test-ship-outbox.sh`
Expected: `OK`

```bash
chmod +x scripts/bugreport/ship-outbox.sh scripts/bugreport/tests/test-ship-outbox.sh
git add scripts/bugreport
git commit -m "feat(bugreport): outboxからMac mini inboxへ運ぶ運搬スクリプトとlaunchd"
```

---

### Task 2: logs repo のレイアウトと `.gitignore`（別repo・PR必須）

**Files:**
- Create（`../moorestech_logs`）: `harness/bug-report/README.md`・`harness/bug-report/inbox/.gitkeep`・`harness/bug-report/runs/.gitkeep`
- Modify（`../moorestech_logs`）: `.gitignore`・`README.md`（レイアウト表に1行追加）

- [ ] **Step 1: ブランチを切って追加する**

```bash
cd ../moorestech_logs && git fetch origin && git checkout -b feature/bug-report-inbox origin/master
mkdir -p harness/bug-report/inbox harness/bug-report/runs && touch harness/bug-report/inbox/.gitkeep harness/bug-report/runs/.gitkeep
cat >> .gitignore <<'GI'
# バグ報告バンドル: inbox は運搬中の一時置き場、動画・連番・未追跡コピー・git bundle は容量のため除外
# Bug-report bundles: inbox is transient; videos, frames, untracked copies and git bundles are excluded for size
harness/bug-report/inbox/
harness/bug-report/runs/*/video.mp4
harness/bug-report/runs/*/frames/
harness/bug-report/runs/*/repo/untracked/
harness/bug-report/runs/*/repo/master-untracked/
harness/bug-report/runs/*/repo/*.bundle
GI
```
`harness/bug-report/README.md`:
```markdown
# bug-report

- `inbox/<id>/` — MacBook から rsync で届いた箱（`READY` 付き）。poller が `runs/` へ移す。git 管理外
- `runs/<id>/` — 自動修正ラン1件。バンドル本体（動画・連番・未追跡コピー・bundle は除外）＋ `run.env`・`packets.jsonl`・`replay-check.json`・`observe/`・`claude.out.json`・`fix-result.json`
- 閲覧は本人とエージェントのみ。公開PRには `runs/<id>/` のパスと説明だけを書く（裁定 2026-09-11）
```
`README.md` のレイアウト表に `harness/bug-report/` の行を追加。

- [ ] **Step 2: push して PR を作る**

```bash
git add -A harness/bug-report .gitignore README.md
git commit -m "harness: bug-report の inbox/runs レイアウトと容量除外"
git push -u origin feature/bug-report-inbox
gh pr create --title "harness: bug-report inbox/runs" --body "ADR 0057（moorestech#… plan C）。バンドル置き場と容量除外。" --base master
```
Expected: PR URL が出る。URL を本repoの判断記録に書く。

---

### Task 3: `prepare-run.sh` と `inbox-poller.sh`（Mac mini側・ローカルでは一時 repo で検証）

**Files:**
- Create: `scripts/bugreport/prepare-run.sh`
- Create: `scripts/bugreport/inbox-poller.sh`
- Create: `scripts/bugreport/README-macmini.md`
- Test: `scripts/bugreport/tests/test-prepare-run.sh`・`scripts/bugreport/tests/test-inbox-poller.sh`

**Interfaces:**
- Consumes: Task 1 の inbox レイアウト、plan B の manifest
- Produces:
  - `runs/<id>/run.env`: `WORKTREE=…`、`MASTER_DIR=…`（`server_v8` まで）、`WORLD_DIR=…`、`REPORT_COMMIT=…`、`REPORT_BRANCH=…`、`LATEST_TICK=…`、`COMMIT_MISSING=0|1`、`DIFF_APPLY_FAILED=0|1`
  - `runs/<id>/world/{world.json,map.json,save.json}`
  - `runs/<id>/claude.out.json`、`runs/<id>/fix-result.json`

- [ ] **Step 1: テストを書く（prepare-run）**

`scripts/bugreport/tests/test-prepare-run.sh`:
```bash
#!/usr/bin/env bash
# 一時 bare repo と作業クローンで prepare-run.sh の git 操作列と world/ 組み立てを検証する
# Verifies prepare-run.sh's git sequence and world/ assembly using a temp bare repo and clone
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT

# origin と作業クローン（moorestech 相当）を作る
# Create an origin and a working clone (stands in for moorestech)
git init -q --bare "$TMP/origin.git"
git clone -q "$TMP/origin.git" "$TMP/repo"
( cd "$TMP/repo" && git config user.email t@t && git config user.name t && mkdir -p moorestech_client/Library && echo lib > moorestech_client/Library/x && echo a > a.txt && git add a.txt && git commit -qm base && git branch -M master && git push -q origin master )
# 報告コミットは未push（bundle 経由で届く）
# The report commit is unpushed (arrives via bundle)
( cd "$TMP/repo" && echo b > b.txt && git add b.txt && git commit -qm report )
REPORT_COMMIT="$(git -C "$TMP/repo" rev-parse HEAD)"
git -C "$TMP/repo" update-ref refs/bugreport/r1 "$REPORT_COMMIT"
git -C "$TMP/repo" bundle create "$TMP/commits.bundle" "origin/master..refs/bugreport/r1" -q
git -C "$TMP/repo" update-ref -d refs/bugreport/r1
git -C "$TMP/repo" checkout -q master

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
{"repository":{"commit":"$REPORT_COMMIT","branch":"feature/x","dirty":true},"masterData":{"commit":"$MASTER_COMMIT","dirty":false},"snapshotTicks":[600,1200]}
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
[ "$LATEST_TICK" = "1200" ] || { echo "NG: LATEST_TICK=$LATEST_TICK"; exit 1; }
[ "$(cat "$WORLD_DIR/save.json")" = '{"currentTick":1200}' ] || { echo "NG: save.json"; exit 1; }
[ "$COMMIT_MISSING" = "0" ] && [ "$DIFF_APPLY_FAILED" = "0" ] || { echo "NG: フラグ"; exit 1; }
echo OK
```

- [ ] **Step 2: `prepare-run.sh` を書く**

```bash
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

json() { python3 -c "import json,sys;d=json.load(open(sys.argv[1]));print(eval(sys.argv[2]))" "$RUN/manifest.json" "$1"; }
REPORT_COMMIT="$(json "d['repository']['commit']")"
REPORT_BRANCH="$(json "d['repository']['branch']")"
MASTER_COMMIT="$(json "d['masterData']['commit']")"
LATEST_TICK="$(json "max(d['snapshotTicks'])")"

WORKTREE="$WORKTREES/bugfix-$ID"; COMMIT_MISSING=0; DIFF_APPLY_FAILED=0
git -C "$REPO" fetch -q origin master
[ -f "$RUN/repo/commits.bundle" ] && git -C "$REPO" fetch -q "$RUN/repo/commits.bundle" '+refs/bugreport/*:refs/bugreport/*' 2>/dev/null || true
if git -C "$REPO" cat-file -e "$REPORT_COMMIT^{commit}" 2>/dev/null; then base="$REPORT_COMMIT"; else base="origin/master"; COMMIT_MISSING=1; log "報告コミットが無いため origin/master を土台にする"; fi
git -C "$REPO" worktree add -q -b "bugfix/$ID" "$WORKTREE" "$base"
if [ -s "$RUN/repo/head.diff" ]; then
  git -C "$WORKTREE" apply --whitespace=nowarn "$RUN/repo/head.diff" || { DIFF_APPLY_FAILED=1; log "head.diff の適用に失敗"; }
fi
[ -d "$RUN/repo/untracked" ] && cp -R "$RUN/repo/untracked/." "$WORKTREE/"

# master data も報告時の実チェックアウト値で worktree を切る（ピンではなく manifest の値）
# The master-data worktree also uses the manifest's actual checkout, not the pin
MASTER_DIR=""
if [ -n "$MASTER_COMMIT" ] && [ -d "$MASTER" ]; then
  [ -f "$RUN/repo/master-commits.bundle" ] && git -C "$MASTER" fetch -q "$RUN/repo/master-commits.bundle" '+refs/bugreport/*:refs/bugreport/*' 2>/dev/null || true
  git -C "$MASTER" worktree add -q --detach "$MASTER_WORKTREES/bugfix-$ID" "$MASTER_COMMIT"
  [ -s "$RUN/repo/master.diff" ] && git -C "$MASTER_WORKTREES/bugfix-$ID" apply --whitespace=nowarn "$RUN/repo/master.diff" || true
  MASTER_DIR="$MASTER_WORKTREES/bugfix-$ID/server_v8"
fi

# Library は APFS クローン（AGENTS.md）。無ければ初回インポートに任せる
# Library via APFS clone (AGENTS.md); fall back to a first import when absent
[ -d "$REPO/moorestech_client/Library" ] && [ ! -d "$WORKTREE/moorestech_client/Library" ] && cp -Rc "$REPO/moorestech_client/Library" "$WORKTREE/moorestech_client/Library"

# world/: 最新スナップショットを save.json にして固定ワールド起動できる形にする
# world/: place the latest snapshot as save.json so a fixed-world boot can load it
WORLD_DIR="$RUN/world"; mkdir -p "$WORLD_DIR"
cp "$RUN/snapshots/tick_$LATEST_TICK.json" "$WORLD_DIR/save.json"

cat > "$RUN/run.env" <<ENV
WORKTREE=$WORKTREE
MASTER_DIR=$MASTER_DIR
WORLD_DIR=$WORLD_DIR
REPORT_COMMIT=$REPORT_COMMIT
REPORT_BRANCH=$REPORT_BRANCH
LATEST_TICK=$LATEST_TICK
COMMIT_MISSING=$COMMIT_MISSING
DIFF_APPLY_FAILED=$DIFF_APPLY_FAILED
ENV
log "prepared: $RUN/run.env"
```

- [ ] **Step 3: `inbox-poller.sh` とテストを書く**

`scripts/bugreport/tests/test-inbox-poller.sh`:
```bash
#!/usr/bin/env bash
# claude と prepare を差し替えて、inbox → runs 移動・result・logs commit を検証する
# Verifies inbox→runs move, result file and the logs commit with claude/prepare stubbed
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
LOGS="$TMP/logs"; mkdir -p "$LOGS/harness/bug-report/inbox/20260911_120000_aaaa1111" "$LOGS/harness/bug-report/runs"
( cd "$LOGS" && git init -q && git config user.email t@t && git config user.name t && echo x > .gitkeep && git add . && git commit -qm init )
echo '{"description":"x"}' > "$LOGS/harness/bug-report/inbox/20260911_120000_aaaa1111/manifest.json"
touch "$LOGS/harness/bug-report/inbox/20260911_120000_aaaa1111/READY"
mkdir -p "$LOGS/harness/bug-report/inbox/20260911_130000_partial.partial"

cat > "$TMP/claude" <<'SH'
#!/usr/bin/env bash
echo '{"result":"stub"}'
SH
cat > "$TMP/prepare" <<'SH'
#!/usr/bin/env bash
echo "WORKTREE=/tmp" > "$MOORESTECH_LOGS/harness/bug-report/runs/$1/run.env"
SH
chmod +x "$TMP/claude" "$TMP/prepare"

MOORESTECH_LOGS="$LOGS" CLAUDE_CMD="$TMP/claude" PREPARE_CMD="$TMP/prepare" GIT_PUSH=0 bash "$HERE/../inbox-poller.sh"
RUN="$LOGS/harness/bug-report/runs/20260911_120000_aaaa1111"
[ -d "$RUN" ] || { echo "NG: runs へ移動していない"; exit 1; }
[ ! -e "$LOGS/harness/bug-report/inbox/20260911_120000_aaaa1111" ] || { echo "NG: inbox に残っている"; exit 1; }
[ -d "$LOGS/harness/bug-report/inbox/20260911_130000_partial.partial" ] || { echo "NG: .partial を触った"; exit 1; }
[ -f "$RUN/claude.out.json" ] || { echo "NG: claude 出力が無い"; exit 1; }
grep -q '"status": *"failure"' "$RUN/fix-result.json" || { echo "NG: result 無しの補完が無い"; exit 1; }
git -C "$LOGS" log --oneline | grep -q "bug-report run 20260911_120000_aaaa1111" || { echo "NG: logs commit"; exit 1; }
echo OK
```

`scripts/bugreport/inbox-poller.sh`:
```bash
#!/usr/bin/env bash
# inbox から1件取り出し、隔離 worktree を用意して自動修正ランを起動する。単一飛行（サーバーポート固定のため）
# Takes one box from the inbox, prepares an isolated worktree and launches the auto-fix run; single flight (fixed server port)
set -euo pipefail
LOGS="${MOORESTECH_LOGS:-$HOME/hermes-agent/data/repos/moorestech_logs}"
BASE="$LOGS/harness/bug-report"; INBOX="$BASE/inbox"; RUNS="$BASE/runs"
REPO="${MOORESTECH_REPO:-$HOME/hermes-agent/data/repos/moorestech}"
CLAUDE_CMD="${CLAUDE_CMD:-claude}"
PREPARE_CMD="${PREPARE_CMD:-$(cd "$(dirname "$0")" && pwd)/prepare-run.sh}"
GIT_PUSH="${GIT_PUSH:-1}"
LOCK="${TMPDIR:-/tmp}/moorestech-bugreport-poller.lock"
log() { echo "[poller] $*" >&2; }

mkdir "$LOCK" 2>/dev/null || { log "別のランが進行中（$LOCK）"; exit 0; }
trap 'rmdir "$LOCK"' EXIT

shopt -s nullglob
boxes=( "$INBOX"/*/READY ); [ ${#boxes[@]} -eq 0 ] && exit 0
box="$(dirname "${boxes[0]}")"; id="$(basename "$box")"
mkdir -p "$RUNS"; mv "$box" "$RUNS/$id"; run="$RUNS/$id"
log "run start: $id"

MOORESTECH_LOGS="$LOGS" "$PREPARE_CMD" "$id" || log "prepare 失敗（続行してエージェントに判断させる）"
. "$run/run.env" 2>/dev/null || WORKTREE="$REPO"

# 非対話で起動し、終了まで待つ。上限は設けない（裁定）
# Launch non-interactively and wait; no time budget (ruling)
( cd "$WORKTREE" && BUG_REPORT_RUNDIR_BASE="$RUNS" $CLAUDE_CMD -p "【無人起動】/bug-report-auto-fix $id" --permission-mode bypassPermissions --output-format json > "$run/claude.out.json" 2> "$run/claude.err.log" ) || log "claude 異常終了（exit $?）"

if [ ! -f "$run/fix-result.json" ]; then
  printf '{"status": "failure", "summary": "claude exited without fix-result.json", "remaining": "runs/%s/claude.err.log を確認"}\n' "$id" > "$run/fix-result.json"
fi
log "run end: $id status=$(python3 -c "import json,sys;print(json.load(open(sys.argv[1])).get('status'))" "$run/fix-result.json")"

( cd "$LOGS" && git add "harness/bug-report/runs/$id" && git commit -qm "bug-report run $id" && { [ "$GIT_PUSH" = "1" ] && git push -q || true; } ) || log "logs commit/push 失敗"
```

`scripts/bugreport/README-macmini.md`:
```markdown
# バグ報告の自動修正ラン（Mac mini側）

- 前提: `~/hermes-agent/data/repos/{moorestech,moorestech_logs,moorestech_master}` が存在し、`claude`・`gh`・`uloop`・`ffmpeg` が PATH にある
- 登録: always-on supervisor の periodic に `bash ~/hermes-agent/data/repos/moorestech/scripts/bugreport/inbox-poller.sh` を60秒周期で追加する（`.decisions/2026-08-14-独立レビュー無人化はsupervisor素pollerを起点にする.md` の poller と同じ置き方。`dev-server-reaper` の対象になるか R11 で確認）
- 手動実行: `bash scripts/bugreport/inbox-poller.sh`。ロック `$TMPDIR/moorestech-bugreport-poller.lock`
- 成果物: `moorestech_logs/harness/bug-report/runs/<id>/`（`fix-result.json`・`claude.out.json`・`observe/`・`replay-check.json`・`packets.jsonl`）
- worktree は `~/hermes-agent/data/repos/moorestech-worktrees/bugfix-<id>`。完了後も消さない（裁定 2026-08-17）
```

- [ ] **Step 4: テスト・コミット**

Run: `bash scripts/bugreport/tests/test-prepare-run.sh && bash scripts/bugreport/tests/test-inbox-poller.sh`
Expected: `OK` ×2

```bash
chmod +x scripts/bugreport/*.sh scripts/bugreport/tests/*.sh
git add scripts/bugreport
git commit -m "feat(bugreport): Mac mini inbox poller と隔離worktreeを用意するprepare-run"
```

---

### Task 4: 再現ツール（`PacketLogJsonDumper`・EDCスニペット・観察シナリオ）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Boot/Replay/PacketLogJsonDumper.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Boot/Replay/BugReportBundleTools.cs`（EDCスニペットから1行で呼ぶ入口。スニペット側に `System.IO` を書かない）
- Create: `.agents/skills/bug-report-auto-fix/scripts/edc/dump-packets.cs`
- Create: `.agents/skills/bug-report-auto-fix/scripts/edc/replay-check.cs`
- Create: `.agents/skills/bug-report-auto-fix/scripts/scenarios/bug-report-observe.cs`
- Create: `.agents/skills/bug-report-auto-fix/scripts/run-edc.sh`（`uloop execute-dynamic-code --code-file` を `BUNDLE` 置換付きで呼ぶ薄いラッパ）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/Replay/PacketLogJsonDumperTest.cs`

**Interfaces:**
- Consumes: plan A `ReceivedPacketLogReader`・`SnapshotReplayer`・`SnapshotJsonComparer`・`WorldSnapshotRing`／`ReplayPacketEntry`
- Produces:
  - `public static class Server.Boot.Replay.PacketLogJsonDumper { public static int Dump(IEnumerable<string> segmentFilePaths, string outputJsonlPath); }`（戻り値はレコード数。各行 `{"tick":<ulong>,"tag":"<string>","json":<MessagePackのJSON>}`）
  - `public static class Server.Boot.Replay.BugReportBundleTools { public static string DumpPackets(string bundleDirectory); public static string ReplayCheck(string bundleDirectory, string serverDataDirectory); }`（`packets.jsonl`／`replay-check.json` を書き、要約文字列を返す）
  - `run-edc.sh <project> <snippet.cs> <bundle-dir> [<server-dir>]`: スニペット内の `__BUNDLE__`／`__SERVER_DIR__` を置換して EDC 実行し JSON 応答を表示
  - `packets.jsonl`・`replay-check.json`（`{"allEqual":bool,"pairs":[{"from":..,"to":..,"equal":bool,"replayedPackets":..,"differences":[..]}]}`）
  - 観察ラン `observe/`（`result.json`・`recording.mp4`・`*.png`）

- [ ] **Step 1: 失敗するテストを書く**

`Tests/CombinedTest/Server/Replay/PacketLogJsonDumperTest.cs`:
```csharp
using System;
using System.IO;
using System.Linq;
using Game.SaveLoad.Snapshot;
using MessagePack;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot.Replay;
using Server.Protocol.PacketResponse;

namespace Tests.CombinedTest.Server.Replay
{
    public class PacketLogJsonDumperTest
    {
        [Test]
        public void パケットログをtickとタグ付きのJSON行へ書き出す()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"moorestech-dump-{Guid.NewGuid():N}");
            var log = new ReceivedPacketLog();
            log.Start(dir, 1);
            log.Append(5, MessagePackSerializer.Serialize(new SaveProtocol.SaveProtocolMessagePack()));
            log.Append(7, MessagePackSerializer.Serialize(BugReportCaptureProtocol.BugReportCaptureRequest.CreateCaptureNowRequest()));
            log.Flush();

            var output = Path.Combine(dir, "packets.jsonl");
            var count = PacketLogJsonDumper.Dump(log.SegmentFilePaths(), output);
            Assert.AreEqual(2, count);
            var lines = File.ReadAllLines(output).Select(JObject.Parse).ToList();
            Assert.AreEqual(5UL, (ulong)lines[0]["tick"]);
            Assert.AreEqual(SaveProtocol.ProtocolTag, (string)lines[0]["tag"]);
            Assert.AreEqual(BugReportCaptureProtocol.ProtocolTag, (string)lines[1]["tag"]);
            Assert.IsTrue(lines[1]["json"].ToString().Contains("va:bugReportCapture"));
            Directory.Delete(dir, true);
        }
    }
}
```

- [ ] **Step 2: `PacketLogJsonDumper` を実装する**

```csharp
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.SaveLoad.Snapshot;
using MessagePack;
using Newtonsoft.Json.Linq;
using Server.Protocol;

namespace Server.Boot.Replay
{
    // パケットログを人とエージェントが読める JSON Lines にする。tag は ProtocolMessagePackBase の Key(0)
    // Turns a packet log into JSON Lines readable by people and agents; tag is Key(0) of ProtocolMessagePackBase
    public static class PacketLogJsonDumper
    {
        public static int Dump(IEnumerable<string> segmentFilePaths, string outputJsonlPath)
        {
            var records = ReceivedPacketLogReader.ReadAll(segmentFilePaths);
            var builder = new StringBuilder();
            foreach (var record in records)
            {
                var tag = MessagePackSerializer.Deserialize<ProtocolMessagePackBase>(record.Payload).Tag;
                var json = MessagePackSerializer.ConvertToJson(record.Payload);
                var line = new JObject { ["tick"] = record.Tick, ["tag"] = tag, ["json"] = JToken.Parse(json) };
                builder.Append(line.ToString(Newtonsoft.Json.Formatting.None)).Append('\n');
            }
            File.WriteAllText(outputJsonlPath, builder.ToString());
            return records.Count;
        }
    }
}
```

- [ ] **Step 3: EDCスニペットとラッパを書く**

`scripts/run-edc.sh`:
```bash
#!/usr/bin/env bash
# バンドルとサーバーディレクトリをスニペットへ埋め込んで uloop execute-dynamic-code を実行する
# Substitutes the bundle and server directory into a snippet and runs it via uloop execute-dynamic-code
set -euo pipefail
PROJECT="$1"; SNIPPET="$2"; BUNDLE="$3"; SERVER_DIR="${4:-}"
TMP="$(mktemp -t edc).cs"
sed -e "s|__BUNDLE__|$BUNDLE|g" -e "s|__SERVER_DIR__|$SERVER_DIR|g" "$SNIPPET" > "$TMP"
uloop execute-dynamic-code --project-path "$PROJECT" --code-file "$TMP"
rm -f "$TMP"
```

`Server.Boot/Replay/BugReportBundleTools.cs`（Step 2 と同じタスクで実装）:
```csharp
using System.IO;
using System.Linq;
using Game.SaveLoad.Snapshot;
using Newtonsoft.Json.Linq;

namespace Server.Boot.Replay
{
    // バンドル1箱に対する再現ツールの入口。EDC スニペットはこれを1行呼ぶだけにする
    // Entry points of the reproduction tools for one bundle; EDC snippets call these in one line
    public static class BugReportBundleTools
    {
        public static string DumpPackets(string bundleDirectory)
        {
            var snapshots = Path.Combine(bundleDirectory, "snapshots");
            var segments = Directory.GetFiles(snapshots, "packets_*.bin").OrderBy(p => p).ToList();
            var count = PacketLogJsonDumper.Dump(segments, Path.Combine(bundleDirectory, "packets.jsonl"));
            return $"packets.jsonl written: {count} records from {segments.Count} segments";
        }

        public static string ReplayCheck(string bundleDirectory, string serverDataDirectory)
        {
            var snapshots = Path.Combine(bundleDirectory, "snapshots");
            var ticks = Directory.GetFiles(snapshots, "tick_*.json")
                .Select(p => ulong.Parse(Path.GetFileNameWithoutExtension(p).Substring(5))).OrderBy(t => t).ToList();
            var segments = Directory.GetFiles(snapshots, "packets_*.bin").OrderBy(p => p).ToList();
            var pairs = new JArray();
            var allEqual = true;
            for (var i = 0; i + 1 < ticks.Count; i++)
            {
                var from = ticks[i];
                var to = ticks[i + 1];
                var result = SnapshotReplayer.Replay(new ReplayRequest(serverDataDirectory, Path.Combine(snapshots, $"tick_{from}.json"), segments, to));
                var comparison = SnapshotJsonComparer.Compare(File.ReadAllText(Path.Combine(snapshots, $"tick_{to}.json")), result.SnapshotJson);
                allEqual &= comparison.Equal;
                pairs.Add(new JObject
                {
                    ["from"] = from, ["to"] = to, ["equal"] = comparison.Equal, ["replayedPackets"] = result.ReplayedPacketCount,
                    ["differences"] = new JArray(comparison.Differences.Take(50)),
                });
            }
            File.WriteAllText(Path.Combine(bundleDirectory, "replay-check.json"), new JObject { ["allEqual"] = allEqual, ["pairs"] = pairs }.ToString());
            return $"replay-check.json written: allEqual={allEqual} pairs={pairs.Count}";
        }
    }
}
```

`scripts/edc/dump-packets.cs`:
```csharp
using Server.Boot.Replay;
return BugReportBundleTools.DumpPackets(@"__BUNDLE__");
```

`scripts/edc/replay-check.cs`:
```csharp
using Server.Boot.Replay;
return BugReportBundleTools.ReplayCheck(@"__BUNDLE__", @"__SERVER_DIR__");
```

`scripts/scenarios/bug-report-observe.cs`:
```csharp
// 報告時点のワールドを起動し、報告者の位置から30秒観察して録画とスクショを残す
// Boot the reported world, observe 30 seconds from the reporter's position, keep the recording and screenshots
using Client.Playtest;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.IO;
using UnityEngine;

var bundle = @"__BUNDLE__";
var manifest = JObject.Parse(File.ReadAllText(Path.Combine(bundle, "manifest.json")));
var player = manifest["clientState"]["playerPosition"];
var position = new Vector3((float)player["x"], (float)player["y"], (float)player["z"]);
var options = new PlaytestRunOptions { Record = true };

return PlaytestRunner.Run("bug-report-observe", options, async p =>
{
    await p.SkipOpeningSkitIfPlaying();
    p.Note($"報告者の位置へワープ: {position}");
    p.WarpPlayer(position);
    await p.WaitSeconds(1f);
    await p.Screenshot("start");
    p.Note((string)manifest["description"]);
    await p.WaitSeconds(15f);
    await p.Screenshot("mid");
    await p.WaitSeconds(15f);
    await p.Screenshot("end");
    p.Assert(true, "観察完了");
});
```
（`clientState.playerPosition` は `Vector3` の Newtonsoft 出力（`x`・`y`・`z`）。plan B の manifest テストで形を確認済み）

- [ ] **Step 4: コンパイル・テスト**

Run: `uloop launch ./moorestech_client --restart` → `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Tests\.CombinedTest\.Server\.Replay\.PacketLogJsonDumperTest$"`
Expected: PASS

- [ ] **Step 5: 実バンドルで3ツールを通す**

plan B の実機確認で outbox に書かれた箱（`$B`）を使う:
```bash
B=~/Library/Application\ Support/moorestech/BugReports/outbox/<最新の箱>
S=.agents/skills/bug-report-auto-fix/scripts
bash $S/run-edc.sh ./moorestech_client $S/edc/dump-packets.cs "$B"
bash $S/run-edc.sh ./moorestech_client $S/edc/replay-check.cs "$B" /Users/katsumi/moorestech_master/server_v8
head -3 "$B/packets.jsonl"; python3 -c "import json;print(json.load(open('$B/replay-check.json'))['allEqual'])"
mkdir -p /tmp/observe-world && cp "$B"/world/*.json /tmp/observe-world/ && cp "$B"/snapshots/$(ls "$B"/snapshots | grep tick_ | sort -t_ -k2 -n | tail -1) /tmp/observe-world/save.json
uloop control-play-mode --project-path ./moorestech_client --action stop
sed "s|__BUNDLE__|$B|g" $S/scenarios/bug-report-observe.cs > /tmp/observe.cs
PLAYTEST_WORLD_DIRECTORY=/tmp/observe-world PLAYTEST_MAP_MODE=template PLAYTEST_SEED=0 \
  .agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh ./moorestech_client /tmp/observe.cs /Users/katsumi/moorestech_master/server_v8
```
Expected: `packets.jsonl` に `va:` タグ行、`allEqual` が `True`（`False` なら差分パスを判断記録に書き、plan A の決定性是正へ戻す）、観察ランが `Success: true` で `recording.mp4` が0byteでない。

- [ ] **Step 6: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Server.Boot/Replay moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/Replay/PacketLogJsonDumperTest.cs .agents/skills/bug-report-auto-fix/scripts
git commit -m "feat(bugreport): パケットログのJSON化・決定性検査・観察シナリオの再現ツール"
```

---

### Task 5: 無人関所の拡張（`unattended-gate.py` に `bugfix`）

**Files:**
- Modify: `.agents/skills/pr-independent-review/scripts/unattended-gate.py`
- Test: `scripts/tests/test_unattended_gate_bugfix.py`

**Interfaces:**
- Produces: `python3 unattended-gate.py stop bugfix` が `BUG_REPORT_RUNDIR_BASE/<id>/fix-result.json`（または `abort.json`）の有無で block／pass。`ask` は従来どおり deny

- [ ] **Step 1: テストを書く**

`scripts/tests/test_unattended_gate_bugfix.py`:
```python
#!/usr/bin/env python3
"""bugfix ジョブの関所を、偽 transcript と一時 run ディレクトリで検証する。
Verifies the bugfix gate with a fake transcript and a temp run directory."""
import json
import os
import subprocess
import sys
import tempfile

GATE = os.path.join(os.path.dirname(__file__), "..", "..", ".agents", "skills", "pr-independent-review", "scripts", "unattended-gate.py")


def run_gate(rundir_base, transcript, session):
    payload = json.dumps({"session_id": session, "transcript_path": transcript})
    env = dict(os.environ, BUG_REPORT_RUNDIR_BASE=rundir_base, TMPDIR=tempfile.mkdtemp())
    return subprocess.run([sys.executable, GATE, "stop", "bugfix"], input=payload, text=True, capture_output=True, env=env)


def main():
    base = tempfile.mkdtemp()
    run = os.path.join(base, "20260911_120000_aaaa1111")
    os.makedirs(run)
    transcript = os.path.join(base, "t.jsonl")
    with open(transcript, "w", encoding="utf-8") as f:
        f.write(json.dumps({"type": "user", "message": {"content": "【無人起動】/bug-report-auto-fix 20260911_120000_aaaa1111"}}) + "\n")

    blocked = run_gate(base, transcript, "s1")
    assert '"block"' in blocked.stdout, f"result 無しなのに block されない: {blocked.stdout!r}"

    with open(os.path.join(run, "fix-result.json"), "w", encoding="utf-8") as f:
        f.write('{"status":"fixed"}')
    passed = run_gate(base, transcript, "s2")
    assert passed.stdout.strip() == "", f"result 有りなのに block された: {passed.stdout!r}"
    print("OK")


if __name__ == "__main__":
    main()
```

- [ ] **Step 2: 関所を拡張する**

`unattended-gate.py` に追加:
- 定数: `BUG_REPORT_RUNDIR_BASE = os.environ.get("BUG_REPORT_RUNDIR_BASE", "/Users/sakastudio/hermes-agent/data/repos/moorestech_logs/harness/bug-report/runs")`、`BUGFIX_PROMPT_RE = re.compile(r"/bug-report-auto-fix\s+([A-Za-z0-9_-]+)")`
- `main()` の `job` 分岐:
  ```python
      elif job == "bugfix":
          pattern = BUGFIX_PROMPT_RE
  ```
  run 解決:
  ```python
      elif job == "bugfix":
          run = os.path.join(BUG_REPORT_RUNDIR_BASE, match.group(1))
  ```
  goal:
  ```python
      elif job == "bugfix":
          goal = "fix-result.json"
  ```
- block メッセージ（既存の `stop` ブロック文の分岐）に bugfix 用の文言を追加: 「`fix-result.json` を書くまで終われない。再現不能・仕様曖昧なら `status` をそれぞれ `not_reproduced` / `needs_ruling` にして summary と remaining を書く」。
- `ask` の stderr 文言に「bugfix なら `fix-result.json` の `status: needs_ruling` に判断事項を書く」を追記。

Run: `python3 scripts/tests/test_unattended_gate_bugfix.py`
Expected: `OK`

- [ ] **Step 3: コミットする**

```bash
git add .agents/skills/pr-independent-review/scripts/unattended-gate.py scripts/tests/test_unattended_gate_bugfix.py
git commit -m "feat(bugreport): 無人関所にbugfixジョブ（fix-result.jsonで終える）を追加"
```

---

### Task 6: スキル `bug-report-auto-fix`

**Files:**
- Create: `.agents/skills/bug-report-auto-fix/SKILL.md`
- Create: `.agents/skills/bug-report-auto-fix/references/fix-result.md`（結果ファイルの契約と3分岐の書き分け）

**Interfaces:**
- Consumes: Task 3 `run.env`、Task 4 ツール、Task 5 関所、`debug-workflow`・`moores-code-review`・`pr-create`・`creating-server-tests`・`unity-playmode-recorded-playtest`・`beads` スキル
- Produces: `fix-result.json`、draft PR、bd issue

- [ ] **Step 1: SKILL.md を書く**

````markdown
---
name: bug-report-auto-fix
description: |
  バグ報告バンドル（ADR 0057）1件を受け取り、隔離worktreeで決定性検査→再現→修正→合成NUnit→draft PRまでを無人で行う。
  Use When: `/bug-report-auto-fix <run-id>` で起動された時（Mac mini の inbox poller が起動する。人が手で起動してもよい）
hooks:
  # 無人実行の関所。fix-result.json を書くまで終われず、AskUserQuestion は deny
  # Unattended gate: cannot stop until fix-result.json exists; AskUserQuestion is denied
  PreToolUse:
    - matcher: "AskUserQuestion"
      hooks:
        - type: command
          command: "python3 .claude/skills/pr-independent-review/scripts/unattended-gate.py ask"
  Stop:
    - hooks:
        - type: command
          command: "python3 .claude/skills/pr-independent-review/scripts/unattended-gate.py stop bugfix"
---

# bug-report-auto-fix — バグ報告の自動再現・修正（無人実行）

`$RUN = $BUG_REPORT_RUNDIR_BASE/<run-id>`（既定 `~/hermes-agent/data/repos/moorestech_logs/harness/bug-report/runs/<run-id>`）。
`$RUN/run.env` に `WORKTREE`・`MASTER_DIR`・`WORLD_DIR`・`REPORT_COMMIT`・`REPORT_BRANCH`・`LATEST_TICK`・`COMMIT_MISSING`・`DIFF_APPLY_FAILED` がある。作業は必ず `$WORKTREE` で行う。

## HARD GATE

- **終端は `$RUN/fix-result.json` を書いた直後だけ**（references/fix-result.md）。書く前に終わる終わり方はバグ
- **3分岐で止まる**: 再現できた→修正まで／再現できない→`not_reproduced`／仕様の曖昧さに当たった→`needs_ruling`（コードを触らず bd に質問を積む）
- **マージしない**。PR は draft
- **時間・利用枠の上限は設けない**（裁定 2026-09-11）。行き詰まりの判定は上の3分岐のみ
- **修正対象は報告されたバグだけ**。ついでの改善はしない（見つけた別問題は bd に積む）

## Step 1: 読む

1. `$RUN/manifest.json`（説明文・`snapshotTicks`・`missing`・`clientState`・`repository`）を読む。`COMMIT_MISSING=1` / `DIFF_APPLY_FAILED=1` なら再現環境が報告時と違うことを summary に必ず書く
2. `$RUN/logs/unity.log` の Error/Exception 行、`$RUN/frames/`（2fps の連番。Read で数枚見る）、`$RUN/screenshot.png`
3. パケットログを可読化: `bash .agents/skills/bug-report-auto-fix/scripts/run-edc.sh $WORKTREE/moorestech_client .agents/skills/bug-report-auto-fix/scripts/edc/dump-packets.cs $RUN`（Editor が未起動なら先に `uloop launch $WORKTREE/moorestech_client`）。`$RUN/packets.jsonl` の末尾（報告直前の操作）を読む
4. `bd create "bug-report <run-id>: <説明文の要約>" --type=bug --priority=2 --description="<manifest要約と $RUN パス>"` で追跡 issue を作り `bd update <id> --claim`

## Step 2: 決定性検査（必須・最初に）

`bash .agents/skills/bug-report-auto-fix/scripts/run-edc.sh $WORKTREE/moorestech_client .agents/skills/bug-report-auto-fix/scripts/edc/replay-check.cs $RUN $MASTER_DIR`
→ `$RUN/replay-check.json`。`allEqual=false` なら **発散した DataStore の是正を先に行う**（ADR 0057）。差分パスが指す箇所の非決定性（列挙順・未シード乱数・未保存の過渡状態）を直し、再検査で `allEqual=true` にしてから Step 3 へ。是正はバグ修正と同じ PR に含め、summary に書く。

## Step 3: 観察（スナップショットからのプレイテスト）

```bash
sed "s|__BUNDLE__|$RUN|g" .agents/skills/bug-report-auto-fix/scripts/scenarios/bug-report-observe.cs > $RUN/observe.cs
uloop control-play-mode --project-path $WORKTREE/moorestech_client --action stop
PLAYTEST_WORLD_DIRECTORY=$WORLD_DIR PLAYTEST_MAP_MODE=template PLAYTEST_SEED=0 \
  .agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh $WORKTREE/moorestech_client $RUN/observe.cs $MASTER_DIR
```
（`world.json` の `mapMode` が `generated` なら `PLAYTEST_MAP_MODE=generated PLAYTEST_SEED=<world.jsonのseed>`）
結果ディレクトリを `$RUN/observe/` へコピーする。説明文の症状が録画・スクショ・`ErrorLogs` に現れるかを判定する。現れなければ、説明文の操作を DSL で再現する追加シナリオを最大3本まで書いて試す（`references/write-scenario.md`）。

## Step 4: 原因特定

`debug-workflow` スキルを起動する。症状＝説明文＋観察結果、既知の試行＝Step 3、尊重すべき制約＝AGENTS.md。ログ仕込みは `$WORKTREE` 内で行い、Step 3 のシナリオで観察する。

## Step 5: 修正と合成NUnit

1. 修正は `$WORKTREE`（ブランチ `bugfix/<run-id>`）で行う
2. **合成NUnit**（`creating-server-tests`）: ワールドファイルに依存しない最小の再現テストを書き、修正前に落ち・修正後に通ることを `uloop run-tests` で確認する。最小化できない場合はプレイテストシナリオを `$RUN/observe/` に残し、PR 本文に「プレイテスト証拠のみ」と明記する（裁定 2026-09-11）
3. `uloop compile` を通す

## Step 6: 修正先の判定（裁定 2026-09-11）

合成NUnit を作ったら、その1コミットを `origin/master` 基底でも実行する:
```bash
git -C $WORKTREE fetch origin master
git -C $WORKTREE branch bugfix/<run-id>-master origin/master
git -C $WORKTREE cherry-pick --no-commit <合成NUnitのコミット> && git -C $WORKTREE checkout bugfix/<run-id>-master -- . 2>/dev/null || true
```
（実際には `git checkout bugfix/<run-id>-master && git cherry-pick <テストコミット>` で同じ worktree のブランチを切り替え、`uloop compile` 後にテストを実行する）
- master でも落ちる → 修正の土台は `bugfix/<run-id>-master`（master 基底）。修正コミットをそこへ cherry-pick / 適用し、PR は `master` 宛て。`base="master"`
- master では落ちない → 土台は `bugfix/<run-id>`。PR は `REPORT_BRANCH` 宛て（`COMMIT_MISSING=1` なら master 宛てにし summary に書く）。`base="<REPORT_BRANCH>"`

## Step 7: 修正後の証拠

Step 3 のシナリオを修正後のバイナリで再実行し `$RUN/observe-after/` に残す。症状が消えたことを録画・スクショで確認する（レビュー後に修正が変わったら再実施）。

## Step 8: レビューと PR

1. `moores-code-review` を実行し、機械的指摘を反映する。設計判断が要る指摘は PR 本文の「裁定事項」に列挙する（AskUserQuestion は使えない）
2. `git push -u origin <ブランチ>` → `pr-create` スキルで PR を作る（本文: 説明文の引用・原因・修正・合成NUnit・`$RUN` のパス・欠損項目・決定性是正の有無・裁定事項）。**動画やスナップショットは添付しない**（裁定: 公開PRにはパスと説明だけ）
3. `gh pr ready --undo <番号>` で draft にする
4. `bd note <id> "PR #<番号> ..."`

## Step 9: 結果を書いて終える

`$RUN/fix-result.json` を references/fix-result.md の契約で書く。`status` は `fixed` / `not_reproduced` / `needs_ruling` / `failure`。`needs_ruling` の場合は `bd create` で裁定事項を積み（`--type=task --priority=1`、本文に候補と帰結）、その id を `remaining` に書く。書いた直後に終了する。
````

`references/fix-result.md`:
```markdown
# fix-result.json

```json
{"status": "fixed|not_reproduced|needs_ruling|failure", "pr_number": 1234, "base": "master|<branch>", "determinism": "ok|diverged|unchecked", "bd_id": "moorestech-xxxx", "summary": "...", "remaining": "..."}
```

- `fixed`: 合成NUnit（または証拠のみ）と修正が draft PR にある。`pr_number` 必須
- `not_reproduced`: Step 3 で症状が出ず、追加シナリオ3本でも出なかった。`summary` に試したこと、`remaining` に次に試す案
- `needs_ruling`: 期待挙動が仕様として決まっていない。コードは触らない。`remaining` に bd の裁定 issue id
- `failure`: 環境要因（Editor 起動不能・master data 不整合・`COMMIT_MISSING` で差分が当たらない等）。`summary` に原因
- `determinism`: Step 2 の結果。`diverged` は是正できずに残した場合のみ
```

- [ ] **Step 2: スキルのリンクとレビュー**

`.claude/skills` は `.agents/skills` への symlink なので追加作業は不要。`reviewing-skills` スキルで SKILL.md をレビューし、指摘を反映する。

- [ ] **Step 3: コミットする**

```bash
git add .agents/skills/bug-report-auto-fix
git commit -m "feat(bugreport): 無人の自動修正ランスキル bug-report-auto-fix"
```

---

### Task 7: MacBook 上での通しリハーサル（Mac mini を模擬）

- [ ] **Step 1:** plan B で作った実バンドルを使い、ローカルで inbox を模擬する:
  ```bash
  L=/tmp/bugreport-logs && mkdir -p $L/harness/bug-report/inbox $L/harness/bug-report/runs && ( cd $L && git init -q && git commit -q --allow-empty -m init )
  cp -R ~/Library/Application\ Support/moorestech/BugReports/outbox/<箱> $L/harness/bug-report/inbox/
  MOORESTECH_LOGS=$L MOORESTECH_REPO=$PWD MOORESTECH_WORKTREES=/Users/katsumi/moorestech-worktrees MOORESTECH_MASTER=/Users/katsumi/moorestech_master MOORESTECH_MASTER_WORKTREES=/Users/katsumi/moorestech-master-worktrees GIT_PUSH=0 \
    CLAUDE_CMD=claude bash scripts/bugreport/inbox-poller.sh
  ```
  （`claude -p` が本当に走る。`BUG_REPORT_RUNDIR_BASE` は poller が渡す）
- [ ] **Step 2:** `$L/harness/bug-report/runs/<箱>/fix-result.json` を読み、`status` と `summary` を判断記録に書く。`worktree` は `git worktree remove` で片付け、bd issue は close する（リハーサル）。
- [ ] **Step 3:** 失敗した段（prepare／EDC／観察／PR）を直してコミットする。

### Task 8: Mac mini での初回運用（初版完成条件）

- [ ] **Step 1:** Mac mini で本ブランチを取り込み、`scripts/bugreport/README-macmini.md` の手順で poller を supervisor に登録する（この手順は Mac mini 側の作業。本repoには手順書のみ）。
- [ ] **Step 2:** MacBook で `ship-outbox.sh` の launchd を有効にし、Editor で実際に1件報告する。
- [ ] **Step 3:** Mac mini の `runs/<id>/fix-result.json` と draft PR を確認し、`status`・所要時間・Editor が reaper に殺されなかったかを判断記録へ書く。殺された場合は `.decisions/` に裁定を求める項目を作る（自動介入範囲の裁定 `2026-08-14-Unity固着watchdogの自動介入範囲.md` と同系）。

### Task 9: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）

- [ ] **Step 1:** `moores-code-review` を起動し、plan C の範囲（シェル・Python・スキル・`PacketLogJsonDumper`）をレビューする。
- [ ] **Step 2:** 指摘を反映して `bash scripts/bugreport/tests/*.sh`・`python3 scripts/tests/test_unattended_gate_bugfix.py`・`uloop run-tests`（`PacketLogJsonDumperTest`）を再実行する。反映が判定経路（poller のロック・READY判定・関所の goal 判定・prepare のフォールバック）に触れたら Task 7 のリハーサルを再実施する。
- [ ] **Step 3:** 設計判断が要る指摘だけを AskUserQuestion で裁定に出す。

### Task 10: セッション終了可能状態にすること

- [ ] **Step 1:** `git status` で未コミットが無いこと。`../moorestech_logs` の PR がマージ済みか確認。
- [ ] **Step 2:** `bd note moorestech-yoag "plan C 完了: <最終コミット> / 初回運用: status=… 所要=…"`、`bd close moorestech-yoag --reason="ADR 0057 初版完了（Editor→Mac mini→draft PR）"`。
- [ ] **Step 3:** `pr-create` で本repoの PR を作る（plan A〜C を1本の PR とする。本文に ADR 0057 と 3 plan のパス、`moorestech_logs` の PR URL）。

---

## 配置と前例（spec-architecture-review）

| # | 項目 | 配置 | 前例・根拠 |
|---|---|---|---|
| 1 | `ship-outbox.sh`・`inbox-poller.sh`・`prepare-run.sh`・launchd plist・README | `scripts/bugreport/` | `scripts/` は `setup-cef.sh` 等の環境スクリプト置き場。Mac mini 固有パスは env の既定値にし本体は環境非依存 |
| 2 | poller の「起動するだけ」の役割分担 | `inbox-poller.sh` はスキルを起動し結果ファイルを待つだけ | `daily-build-repair` の「poller は修復ロジックを持たない」と同じ |
| 3 | 無人関所 | `pr-independent-review/scripts/unattended-gate.py` に `bugfix` ジョブ追加 | 既に `review`/`apply`/`repair` の3スキルが共用。新規スクリプトを作らず同じ関所に乗る |
| 4 | スキル | `.agents/skills/bug-report-auto-fix/`（正本。`.claude/skills` は symlink） | AGENTS.md「スキルの git 正本は `.agents/skills/` のみ」 |
| 5 | `PacketLogJsonDumper` | `Server.Boot/Replay/` | `SnapshotReplayer` と同じ再現ツール群。`MessagePackSerializer.ConvertToJson` の前例は `Client.Network/ServerCommunicator.cs:95` |
| 6 | EDC スニペット・観察シナリオ | スキル配下 `scripts/edc/`・`scripts/scenarios/` | `unity-playmode-recorded-playtest` がシナリオをスキル同梱にしている前例。プロジェクト側に置かない |
| 7 | 実行記録の置き場 | `moorestech_logs/harness/bug-report/runs/<id>/` | AGENTS.md「実行記録はコードrepoに置かず `../moorestech_logs/harness/` へ」。`pr-independent-review/runs/pr-<n>/` と同型 |
| 8 | 結果ファイル契約 | `fix-result.json` | `daily-build-repair` の `repair-result.json`（`status/pr_number/verified/summary/remaining`）と同型に `base`・`determinism`・`bd_id` を足す |

死活表（Phase 2.5）: 既存の pr-review poller・apply スロット → 触らない（別 inbox・別ロック）／`unattended-gate.py` の既存3ジョブ → 分岐追加のみで挙動不変（テストは既存の `stop review` 経路を1本追加して確認する）／Mac mini メインクローンでの Unity 起動 → しない（worktree のみ、裁定 2026-08-17）。

## 判断記録（ADR）

- 設計ADR: `docs/adr/0057-bug-report-bundle-and-isolated-auto-fix.md`、裁定 `.decisions/2026-09-11-バグ報告*.md`・`2026-09-11-Tailscaleに繋がらない機材の運搬は初版では何もしない.md`・`2026-09-11-バグ修正の届け先は…`・`2026-09-11-バグ報告の自動修正ランは上限なしで…`
- **`claude -p`（非対話）で起動する**（agent前提・ADR 0023 からの逸脱）: 既存 poller の cmux 対話モード起動は `~/hermes-agent/data/services/pr-review/poller.py`（本repo外）の `launch_claude` に依存し、本repoからは再現できない。transcript 監視も持たないため、まず非対話で通し、cmux 化は運用後に裁定する。
- **バンドル本体は logs repo の `runs/` に置くが動画・連番・未追跡コピー・bundle は git 除外**（agent前提）: README の「100MB超ファイルで push が沈黙失敗」「soft limit 5GB」に当たるため。裁定「バンドルはmoorestech_logs側」はディレクトリ配置として満たし、除外物は Mac mini のディスクに残る。
- **未pushコミットは `git bundle` で運ぶ**（agent前提）: 裁定「未コミット差分まで再現対象」を満たすには報告時コミットが Mac mini に要る。push を強いず、運搬スクリプトが `origin/master..<commit>` の bundle を添付する。
- **修正先判定は同一 worktree のブランチ切替で行う**（agent前提）: 2本目の Editor と Library を避ける。
- **poller は直列**（ADR 0057 agent前提の再掲）: サーバーポート11564固定。
- **`dev-server-reaper` との関係は未確認**: Task 8 で実測し、必要なら裁定へ。
- Task 7/8 の結果: （実装時に転記）
