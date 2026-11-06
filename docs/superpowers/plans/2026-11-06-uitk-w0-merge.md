# UITK W0-merge（master を feature/uitk へ取り込み束 1 を暗転へ戻す・albof.20）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** `origin/master` を長命ブランチ `feature/uitk` へマージコミットで取り込み、台帳 `docs/uitk/TODO.md` の衝突を解いて束 1 を `暗転`・束 1 の 3 行の所有者を `Web` に戻し、未記帳 2 件を記帳し、UITK 移行の実行計画一式（`docs/uitk-migration/execution/` と詳細計画 12 本）を同じ PR で入れる。

**Architecture:** integrator の直列作業（実行計画 §3「W0 の頭」）。Editor を使わない文書と台帳だけのレーン。台帳の衝突は手で 1 行ずつ直さず、このplanに全文を載せた使い捨ての解決スクリプト（コミットしない）で決定的に解き、G1 のスクリプト `.github/scripts/check-uitk-web-follow.py` とその単体テスト 5 本で合否を出す。最後は `base=feature/uitk` の PR を G1 緑で agent がマージコミットで取り込む。

**Tech Stack:** git（マージコミット）、Python 3（G1 スクリプト・解決スクリプト・`unittest`）、GitHub Actions（`UITK Web Follow Gate`）、gh CLI、bd（Beads）

## Requirements

1. `origin/master`（取り込み先の決め方は R2）を、レーンブランチの上で `feature/uitk` の先端へマージコミットで取り込む。受け入れ: `git log -1 --format=%P <マージコミット>` の親が 2 つで、2 つ目が取り込み先の master のコミット
2. 取り込み先は実行時の `origin/master`。ただし (a) `git merge-base` 以降の master に `moorestech_web/` を触る非マージコミットがある、または (b) `origin/master` に CEF 停止の記録（W0-cef）の新規ファイル `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/WebUiHealth/IWebUiHealthProbe.cs` がある場合は、`cb4b23e9d8`（2026-11-06 実測で Web 変更 0・W0-cef 無しの master 先端）に固定し、残りの master は I-weekly へ回すと報告に書く。受け入れ: Task 1 の判定ログに取り込み先 SHA と理由が出る
3. `docs/uitk/TODO.md` の衝突 1（「## 行」の束 1 の 3 行）を、所有者は master 側（`Web`）、pauseMenu の `実測の結論` 列は feature/uitk 側（UITK ポーズの撮影 URL 4 本）、toast の `agent 既定` 列は master 側（「裁定 2=A。見た目は Web 版と同じ（2026-11-06 裁定）」）で解く。受け入れ: 解決後の 3 行が Task 2 Step 4 の期待値と一致
4. 衝突 2（「## agent 既定（ユーザー未裁定）」）を、master 側の「様式化しない」裁定行を採り、「通信失敗のトースト」行を全体スイッチ前提の文（Task 2 の `FAILURE_TOAST_ROW`）へ書き換えて残して解く。受け入れ: 衝突マーカー 0、`様式化 (a)` の文字列 0
5. 束 1 を `暗転`（起点 `979725bcd1` のまま）に戻す。受け入れ: 「## 束」の束 1 行が `| 1 | 基盤と命綱（ポーズ・設定・バグ報告・トースト） | 暗転 | 979725bcd1 | \`uitk-lifeline\` |`、`| UITK |` を含む行 0
6. 「## 追従記録」の束 1 に未記帳 2 件を記帳する: `3e946e4bd3` を `写した`、`5421c7f9f0` を `捨てた`（理由: W0-switch で旧 R1 の Web 側ごと外す）。受け入れ: G1 で束 1 の未記帳 0 件
7. 実行計画一式を同じ PR で入れる: `docs/uitk-migration/execution/execution-plan.md`・`component-architecture.md`・`research/` 5 本（stage1-asbuilt・web-components・merge-delta・tests-ladder・cef-record）と `docs/superpowers/plans/2026-11-06-uitk-{w0-merge,w0-cef,w0-switch,w0-skill,w0-tools,f1,c2-a,c2-b,c2-c,l0-s3,l0-s4a,l0-s4b}.md` の 12 本。受け入れ: Task 1 Step 4 の存在検査が 19 件とも OK、12 本とも `## 設計検査記録` が `実施済み`
8. ローカルで `python3 .github/scripts/check-uitk-web-follow.py --base <取り込み前の feature/uitk> --head HEAD` が「束 1（暗転・起点 979725bcd1）: 未記帳 0 件」「違反なし」・終了コード 0
9. `.github/scripts/tests/test_uitk_web_follow_*.py` 5 本（boundaries 11・gate 18・history 10・ledger 27・real_ledger 4 件。2026-11-06 の模擬マージでの実測件数）が全部 OK
10. `base=feature/uitk` の PR で `UITK Web follow gate (G1)` と `UITK Web follow gate self-test` が緑、agent が `gh pr merge --merge`（Squash 禁止）で取り込む。bd `moorestech-albof.20` を閉じる
- やらないこと: 全体スイッチの作り直し（所有表・`UiOwnershipEditorOverride`・旧 R1 の Web 側変更の撤去、台帳「読み方と運用」末尾の R4 手順の削除）は W0-switch（albof.17）。CEF 停止の記録の取り込み時の解決表（cef-record §6）は W0-cef が master に入った後の I-weekly。`5421c7f9f0` の Web 変更そのものはこのレーンでは外さない（記帳だけ）。C# と Unity アセットは編集しない。参照キャプチャの撮り直しは指揮の作業（実行計画 §3）

## Global Constraints

- 作業場所: 指揮が `moores-wt new uitk-lane/w0-merge --from origin/feature/uitk --no-editor` で作った worktree。全コマンドはその worktree の root を cwd にして実行する（最初に `pwd` で確かめる）。メインワークツリーでは作業しない
- 裁定（`origin/master:.decisions/`）: `2026-11-06-UITK移行は長命ブランチで作り一気に切り替える.md`、`2026-11-06-UITK長命ブランチの中は全体スイッチ1つで切り替え追従はブランチ側で記帳する.md`、`2026-11-06-UITK段階2以降の画面もWeb版と同じ見た目にする.md`、`2026-11-06-UITK段階1の画面はWeb版と同じ見た目にする.md`、`2026-11-06-CEF停止の記録はUITK長命ブランチから先にmasterへ切り出す.md`、`2026-10-26-UITKが安定するまでWeb-UIの変更を続け現行機能をすべてUITKへ移す.md`
- 統合計画の正本は `origin/master:docs/uitk-migration/plan.md`（取り込み後はこのブランチの同ファイル）。§1.1「束 1 の暗転戻し」と §2.2「作業（画面）」2 行目が本レーンの指示
- 台帳の書式: セルに `|` を書かない。空欄は `—`。コミットは 10 桁以上。`捨てた` には理由が要る（`docs/uitk/TODO.md`「## 追従記録」の前文）
- マージは必ずマージコミット（`git merge --no-ff`／`gh pr merge --merge`）。Squash・rebase 禁止（AGENTS.md「タスク管理」）。master への PR は作らない（裁定。例外は W0-cef と切替 PR）
- Unity Editor は起動しない。`.cs` は編集しない。取り込みで `.cs` が入った場合だけ Task 2 Step 6 の手順で compile する（AGENTS.md「コードを書き終わったら必ずコンパイル」は `.cs` 変更限定）
- 解決スクリプトは `${TMPDIR:-/tmp}/uitk-w0-merge/` に置き、コミットしない（使い捨て）。それでもコードは AGENTS.md の規約（200 行未満・日英 2 行コメント・try-catch 不使用）に従う
- バックグラウンドのポーリングで待たず、全部前景で実行する。タスクごとにコミットする
- 報告は `../moorestech_logs/harness/uitk/W0-merge/done.md`（完了条件ごとの証拠・未完了・要裁定）。`../moorestech_logs` で書く前に同 repo の `README.md` を読む

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 書き換えるのは台帳と文書だけで、配置・200行・10ファイルの違反なし
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱1・第3バケツ0 / 解決スクリプトの重複判定（pauseMenu 行・衝突の塊）を is_pause_row と hunk_indexes の 1 か所へまとめた。ADR 矛盾なし

---

## File Structure

| パス（repo 相対） | 変更 | 責務 |
|---|---|---|
| `docs/uitk/TODO.md` | Modify（マージの衝突解決） | 台帳。束 1 の状態・束 1 の 3 行・agent 既定 2 行・追従記録 2 行 |
| `docs/uitk-migration/plan.md`・`.decisions/2026-11-06-*.md`・`.github/workflows/uitk-web-follow-gate.yml`・`moorestech_client/Assets/AddressableResources/Block/CleanRoom*.prefab` | master から自動マージで入る（手で触らない） | master 側の正本 |
| `docs/uitk-migration/execution/**`・`docs/superpowers/plans/2026-11-06-uitk-*.md`（12 本） | `origin/docs/uitk-execution-plan` のマージで入る（手で触らない） | 実行計画一式 |
| `${TMPDIR:-/tmp}/uitk-w0-merge/resolve_todo_w0_merge.py` | Create（コミットしない） | 台帳の衝突を決定的に解く使い捨てスクリプト |
| `${TMPDIR:-/tmp}/uitk-w0-merge/target.env` | Create（コミットしない） | 取り込み前の feature/uitk の SHA と取り込み先の master の SHA |

---

### Task 1: 前提の確認と実行計画一式の取り込み

**Files:**
- Modify（マージで追加）: `docs/uitk-migration/execution/execution-plan.md`・`docs/uitk-migration/execution/component-architecture.md`・`docs/uitk-migration/execution/research/{stage1-asbuilt,web-components,merge-delta,tests-ladder,cef-record}.md`・`docs/superpowers/plans/2026-11-06-uitk-*.md`（12 本）
- Create（コミットしない）: `${TMPDIR:-/tmp}/uitk-w0-merge/target.env`

**Interfaces:**
- Consumes: 指揮が push したブランチ `origin/docs/uitk-execution-plan`（`feature/uitk` から切った docs だけのブランチ）
- Produces: `target.env` の 2 変数 `PRE_FEATURE`（取り込み前の `origin/feature/uitk` の 40 桁 SHA）と `MERGE_TARGET`（取り込む master の 40 桁 SHA）。Task 2・Task 3 がこのファイルを `source` する

- [ ] **Step 1: 場所とブランチを確かめる**

Run:
```bash
pwd
git branch --show-current
git status --short
git fetch origin
```
Expected: `pwd` が `.../worktrees/moorestech/<w0-merge の worktree>`、ブランチ `uitk-lane/w0-merge`、`git status --short` が空。違えば止めて指揮に報告する（メインワークツリーで作業しない）

- [ ] **Step 2: 取り込み先の master を決めて記録する**

Run:
```bash
mkdir -p "${TMPDIR:-/tmp}/uitk-w0-merge"
PRE_FEATURE=$(git rev-parse origin/feature/uitk)
BASE=$(git merge-base origin/master origin/feature/uitk)
WEB_COMMITS=$(git log --no-merges --format=%h "$BASE"..origin/master -- moorestech_web | wc -l | tr -d ' ')
if git cat-file -e origin/master:moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/WebUiHealth/IWebUiHealthProbe.cs 2>/dev/null; then CEF_IN_MASTER=1; else CEF_IN_MASTER=0; fi
if [ "$WEB_COMMITS" = "0" ] && [ "$CEF_IN_MASTER" = "0" ]; then MERGE_TARGET=$(git rev-parse origin/master); REASON="origin/master"; else MERGE_TARGET=$(git rev-parse cb4b23e9d8); REASON="fixed cb4b23e9d8 (web_commits=$WEB_COMMITS cef_in_master=$CEF_IN_MASTER)"; fi
printf 'PRE_FEATURE=%s\nMERGE_TARGET=%s\n' "$PRE_FEATURE" "$MERGE_TARGET" > "${TMPDIR:-/tmp}/uitk-w0-merge/target.env"
echo "base=$BASE pre_feature=$PRE_FEATURE merge_target=$MERGE_TARGET reason=$REASON"
```
Expected（2026-11-06 時点）: `base=979725bcd1…`、`reason=origin/master`、`merge_target=cb4b23e9d8…` 以降の master 先端。`reason=fixed …` になったら、そのログ行を `done.md` の「未完了」に「残りの master（Web 変更 N 件／W0-cef）は I-weekly へ」と書き写す。`base` が `979725bcd1` でなければ（feature/uitk に別の取り込みが入った）止めて指揮に報告する（このplanの衝突の前提が崩れている）

- [ ] **Step 3: 実行計画一式のブランチを取り込む**

Run:
```bash
git merge --no-edit origin/docs/uitk-execution-plan
git diff --name-only "$(grep PRE_FEATURE "${TMPDIR:-/tmp}/uitk-w0-merge/target.env" | cut -d= -f2)" HEAD | grep -v -E '^docs/' || echo "docs-only"
```
Expected: マージが成功し（早送りかマージコミット）、2 行目が `docs-only`。`origin/docs/uitk-execution-plan` が無い（`merge: origin/docs/uitk-execution-plan - not something we can merge`）なら止めて指揮に「実行計画一式のブランチが未 push」と報告する。`docs/` 以外のファイルが出たら止めて報告する

- [ ] **Step 4: 19 本の存在と設計検査の済みを確かめる**

Run:
```bash
for f in docs/uitk-migration/execution/execution-plan.md docs/uitk-migration/execution/component-architecture.md \
  docs/uitk-migration/execution/research/stage1-asbuilt.md docs/uitk-migration/execution/research/web-components.md \
  docs/uitk-migration/execution/research/merge-delta.md docs/uitk-migration/execution/research/tests-ladder.md \
  docs/uitk-migration/execution/research/cef-record.md; do test -f "$f" && echo "OK $f" || echo "MISSING $f"; done
for id in w0-merge w0-cef w0-switch w0-skill w0-tools f1 c2-a c2-b c2-c l0-s3 l0-s4a l0-s4b; do
  f="docs/superpowers/plans/2026-11-06-uitk-$id.md"
  if [ ! -f "$f" ]; then echo "MISSING $f"; elif [ "$(grep -c -E '^- (配置検査|Phase 2\.6).*: 実施済み' "$f")" = "2" ]; then echo "OK $f"; else echo "UNCHECKED $f"; fi
done
```
Expected: 19 行すべて `OK`。`MISSING` か `UNCHECKED` が 1 件でもあれば止めて指揮に報告する（レビューフロー（実行計画 §5）を通っていない計画を入れない）

- [ ] **Step 5: コミット（Step 3 が早送りでマージコミットが無かった場合も含め、作業ツリーが空であることを確かめる）**

Run: `git status --short`
Expected: 空（Step 3 のマージ自体がコミット。追加のコミットは不要）

---

### Task 2: master の取り込みと台帳の解決

**Files:**
- Modify: `docs/uitk/TODO.md`（衝突 2 か所・束 1 の行・追従記録 2 行）
- Create（コミットしない）: `${TMPDIR:-/tmp}/uitk-w0-merge/resolve_todo_w0_merge.py`

**Interfaces:**
- Consumes: Task 1 の `target.env`（`PRE_FEATURE`・`MERGE_TARGET`）
- Produces: マージコミット 1 つ（親 1 = Task 1 後の HEAD、親 2 = `MERGE_TARGET`）。台帳の確定行（Task 2 Step 4 の期待値）。W0-switch が読む「通信失敗のトースト」行の文（`FAILURE_TOAST_ROW`）

- [ ] **Step 1: master をマージし、衝突が台帳 1 ファイルだけであることを確かめる**

Run:
```bash
. "${TMPDIR:-/tmp}/uitk-w0-merge/target.env"
git merge --no-ff --no-commit "$MERGE_TARGET"
git diff --name-only --diff-filter=U
```
Expected: `CONFLICT (content): Merge conflict in docs/uitk/TODO.md` が出て、`--diff-filter=U` の出力が `docs/uitk/TODO.md` の 1 行だけ。ほかのファイルが出たら `git merge --abort` して止め、指揮に衝突一覧を報告する（このplanの範囲外）

- [ ] **Step 2: 解決スクリプトを書く**

`${TMPDIR:-/tmp}/uitk-w0-merge/resolve_todo_w0_merge.py`:

```python
"""W0-merge: docs/uitk/TODO.md の衝突 2 か所を解き、束 1 を暗転へ戻し、未記帳 2 件を記帳する。
W0-merge: resolves the two conflicts in docs/uitk/TODO.md, returns bundle 1 to dark, and records two commits."""
import sys
from pathlib import Path

OURS, MID, THEIRS = "<<<<<<< ", "=======", ">>>>>>> "
FAILURE_TOAST_ROW = (
    "| 通信失敗のトースト | 全体スイッチの作り直し（W0-switch・albof.17）で決める。"
    "スイッチの UITK 側では Web ホストを起動しないので Web の toast は出ない"
    " | UITK のコマンドは WS を通らないので通信失敗は起きない見込み（推測・コード未確認）。"
    "W0-switch が UITK 側に通信失敗の経路が無いことを確かめ、この行を結論で書き換える |"
)
BUNDLE1_SWITCHED = "| 1 | 基盤と命綱（ポーズ・設定・バグ報告・トースト） | 切替済み | 979725bcd1 | `uitk-lifeline` |"
BUNDLE1_DARK = "| 1 | 基盤と命綱（ポーズ・設定・バグ報告・トースト） | 暗転 | 979725bcd1 | `uitk-lifeline` |"
LAST_FOLLOW_ROW_PREFIX = "| 1 | ccb3d512f7 | 写した |"
NEW_FOLLOW_ROWS = [
    "| 1 | 3e946e4bd3 | 写した | D-8 の拒否理由ごとのトースト文言。Web 側は Localization/localization.csv への追加に連動した生成物 src/shared/i18n/generated/localizationKeys.ts の再生成だけで、写し先は同じコミットの C# UiCommandRejectReasonTraits.ToLocalizationKey と localization.csv の文言 |",
    "| 1 | 5421c7f9f0 | 捨てた | 旧 R1（画面別の Web 休止）の入力排他フックを src/shared/uiState/inputExclusivity/ へ移した自動修正。全体スイッチの作り直し（W0-switch・albof.17）で旧 R1 の Web 側変更ごと外して master と同じ Web に戻すので、UITK 側に写すものが無い |",
]


def fail(reason):
    # 前提が崩れたら書き換えずに理由を出して止める
    # Stop without writing and print why when the premise no longer holds
    sys.exit("resolve_todo_w0_merge: " + reason)


def split_hunks(lines):
    # 衝突の塊を (feature 側, master 側) の組にし、塊の外の行はそのまま並べる
    # Turn each conflict hunk into a (feature, master) pair and keep other lines as they are
    out, i = [], 0
    while i < len(lines):
        if not lines[i].startswith(OURS):
            out.append(lines[i])
            i += 1
            continue
        mid = lines.index(MID, i)
        end = next(k for k in range(mid, len(lines)) if lines[k].startswith(THEIRS))
        out.append((lines[i + 1:mid], lines[mid + 1:end]))
        i = end + 1
    return out


def cells(row):
    return [c.strip() for c in row.strip().strip("|").split("|")]


def is_pause_row(row):
    return row.startswith("| pauseMenu")


def resolve_rows_hunk(feature, master):
    # 所有者は master 側（Web）、pauseMenu の実測の結論列だけ feature 側の撮影 URL を残す
    # Take master's owners (Web) and keep only feature's capture URLs in pauseMenu's measured column
    feature_pause = next((r for r in feature if is_pause_row(r)), None)
    if feature_pause is None or len(master) != 3:
        fail("衝突 1 の形が想定（束 1 の 3 行）と違う")
    resolved = []
    for row in master:
        if is_pause_row(row):
            merged = cells(row)
            merged[6] = cells(feature_pause)[6]
            row = "| " + " | ".join(merged) + " |"
        resolved.append(row)
    return resolved


def resolve_defaults_hunk(feature, master):
    # master の様式化しない裁定行を採り、通信失敗のトースト行は全体スイッチ前提へ書き換えて前に置く
    # Take master's no-restyle ruling and put the failure-toast row, rewritten for the global switch, before it
    if not any(r.startswith("| 通信失敗のトースト") for r in feature) or len(master) != 1:
        fail("衝突 2 の形が想定（通信失敗のトースト＋様式化の行）と違う")
    return [FAILURE_TOAST_ROW] + master


def main(ledger_path):
    ledger = Path(ledger_path)
    parts = split_hunks(ledger.read_text(encoding="utf-8").split("\n"))
    hunk_indexes = [i for i, p in enumerate(parts) if isinstance(p, tuple)]
    if len(hunk_indexes) != 2:
        fail(f"衝突の塊が 2 個でない（{len(hunk_indexes)} 個）。merge-delta を取り直して計画を見直す")

    # 塊を順に解き、束 1 を暗転へ戻し、追従記録の末尾に 2 行足す
    # Resolve hunks in order, return bundle 1 to dark, and append two follow records
    resolvers = dict(zip(hunk_indexes, [resolve_rows_hunk, resolve_defaults_hunk]))
    resolved = []
    for i, part in enumerate(parts):
        resolved += resolvers[i](*part) if i in resolvers else [part]
    if resolved.count(BUNDLE1_SWITCHED) != 1:
        fail("束 1 の切替済みの行がちょうど 1 行ではない")
    resolved[resolved.index(BUNDLE1_SWITCHED)] = BUNDLE1_DARK
    anchor = next((i for i, r in enumerate(resolved) if r.startswith(LAST_FOLLOW_ROW_PREFIX)), None)
    if anchor is None:
        fail("追従記録の ccb3d512f7 の行が無い")
    resolved[anchor + 1:anchor + 1] = NEW_FOLLOW_ROWS

    ledger.write_text("\n".join(resolved), encoding="utf-8")
    print("resolved: 衝突 2 か所・束 1 暗転・追従記録 2 行")


main(sys.argv[1])
```

- [ ] **Step 3: スクリプトで台帳を解く**

Run: `python3 "${TMPDIR:-/tmp}/uitk-w0-merge/resolve_todo_w0_merge.py" docs/uitk/TODO.md`
Expected: `resolved: 衝突 2 か所・束 1 暗転・追従記録 2 行`。`resolve_todo_w0_merge: …` で止まったら `git merge --abort` し、その文を添えて指揮に報告する（台帳の形が 2026-11-06 の実測から変わった）

- [ ] **Step 4: 解決後の台帳を検査する**

Run:
```bash
grep -c -E '^(<<<<<<<|=======|>>>>>>>)' docs/uitk/TODO.md
grep -c '| UITK |' docs/uitk/TODO.md
grep -c '様式化 (a)' docs/uitk/TODO.md
grep -n -E '^\| 1 \| 基盤と命綱' docs/uitk/TODO.md
grep -n -E '^\| (settings|toast) ' docs/uitk/TODO.md
grep -c -E '^\| pauseMenu（ポーズ・バグ報告・入れ子のポーズ） \| 機能 \| 1 \| Web \| .* \| 裁定 2=A（第一弾は命綱） \| UITK のポーズ（PlayMode・ScreenCapture・フレーム番号と時刻の透かし入り）: トップ 1280×720 https://' docs/uitk/TODO.md
grep -n -E '^\| 1 \| (3e946e4bd3|5421c7f9f0) ' docs/uitk/TODO.md | cut -c1-40
grep -n -E '^\| (通信失敗のトースト|様式化 Mantine)' docs/uitk/TODO.md | cut -c1-60
```
Expected（順に）:
- `0`、`0`、`0`
- `…:| 1 | 基盤と命綱（ポーズ・設定・バグ報告・トースト） | 暗転 | 979725bcd1 | \`uitk-lifeline\` |`
- `| settings（画質・言語・ダッシュ方式） | 機能 | 1 | Web | \`src/features/settings/\` | 裁定 2=A | — | 1-settings 1-settings-graphics-recovered |` と `| toast | 機能 | 1 | Web | \`src/features/toast/\` \`e2e/tests/regression/actionErrors.spec.ts\` | 裁定 2=A。見た目は Web 版と同じ（2026-11-06 裁定） | — | 1-toast |`
- `1`
- 2 行（`| 1 | 3e946e4bd3 | 写した` と `| 1 | 5421c7f9f0 | 捨てた` がこの順で、`ccb3d512f7` の行の直後）
- 2 行（`| 通信失敗のトースト | 全体スイッチの作り直し…` の次に `| 様式化 Mantine の素の見た目の 4 画面 | 2026-11-06 ユーザー裁定…`）

- [ ] **Step 5: マージコミットを作る**

Run:
```bash
. "${TMPDIR:-/tmp}/uitk-w0-merge/target.env"
git add docs/uitk/TODO.md
git commit -m "Merge origin/master into feature/uitk（albof.20）: 台帳の束 1 を暗転へ戻し、束 1 の 3 行の所有者を Web に、未記帳 2 件（3e946e4bd3 写した・5421c7f9f0 捨てた）を記帳する

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git log -1 --format='%H %P'
```
Expected: 親が 2 つで、2 つ目が `$MERGE_TARGET`

- [ ] **Step 6: 取り込みで .cs が入ったときだけ compile する**

Run: `git diff --name-only HEAD^1 HEAD -- '*.cs' | wc -l`
Expected: `0`（2026-11-06 時点で master 側に C# 変更は無い）。1 以上なら、`moores-wt status` で自分の worktree に Editor が無いことを確かめてから `uloop launch --project-path ./moorestech_client` → `uloop compile --project-path ./moorestech_client`（ErrorCount 0）を実行し、結果を `done.md` に書く。エラーが出たら止めて指揮に報告する（このレーンは C# を直さない）

---

### Task 3: G1 と単体テストでの合否確認

**Files:**
- Test: `.github/scripts/check-uitk-web-follow.py`・`.github/scripts/tests/test_uitk_web_follow_{boundaries,gate,history,ledger,real_ledger}.py`（どれも変更しない）

**Interfaces:**
- Consumes: Task 1 の `PRE_FEATURE`、Task 2 のマージコミット（HEAD）
- Produces: `done.md` に貼る G1 の 2 行とテスト 5 本の `Ran N tests … OK`

- [ ] **Step 1: G1 を取り込みの PR と同じ引数で回す**

Run:
```bash
. "${TMPDIR:-/tmp}/uitk-w0-merge/target.env"
python3 .github/scripts/check-uitk-web-follow.py --base "$PRE_FEATURE" --head HEAD; echo "rc=$?"
python3 .github/scripts/check-uitk-web-follow.py --base 979725bcd1 --head HEAD; echo "rc=$?"
```
Expected（両方とも）:
```
G1: 束 1（暗転・起点 979725bcd1）: 未記帳 0 件
G1: 違反なし
rc=0
```
未記帳が 1 件以上なら、出たコミットを `git show --stat <sha> -- moorestech_web` で見て、Task 1 Step 2 の判定を見直す（`reason=origin/master` のまま master の Web 変更が入っていれば、取り込み先を `cb4b23e9d8` に固定してやり直す: `git reset --hard "$(git rev-parse HEAD^1)"` で Task 2 の前へ戻し、`target.env` の `MERGE_TARGET` を `cb4b23e9d8` の 40 桁へ書き換えて Task 2 を再実行）

- [ ] **Step 2: G1 の単体テスト 5 本を回す**

Run:
```bash
for f in .github/scripts/tests/test_uitk_web_follow_*.py; do
  n=$(basename "$f"); printf '%s: ' "$n"
  python3 -m unittest discover -s .github/scripts/tests -p "$n" 2>&1 | grep -E '^(Ran|OK|FAILED)' | tr '\n' ' '; echo
done
```
Expected:
```
test_uitk_web_follow_boundaries.py: Ran 11 tests in …s OK
test_uitk_web_follow_gate.py: Ran 18 tests in …s OK
test_uitk_web_follow_history.py: Ran 10 tests in …s OK
test_uitk_web_follow_ledger.py: Ran 27 tests in …s OK
test_uitk_web_follow_real_ledger.py: Ran 4 tests in …s OK
```
件数が増えているのは master 側でテストが足された場合で、OK なら可。FAILED が出たら止めて指揮に報告する（台帳の解決が G1 の検査を壊した）

- [ ] **Step 3: 報告ファイルの下書きを書く**

`../moorestech_logs/README.md` を読んでから、`../moorestech_logs/harness/uitk/W0-merge/done.md` に次の見出しで書く（コミットは moorestech_logs の手順に従う）:
```markdown
# W0-merge（albof.20）
## 完了条件ごとの証拠
- 取り込み先: <MERGE_TARGET 10 桁>（理由: <Task 1 Step 2 の reason>）
- マージコミット: <HEAD 10 桁>（親: <親 1>・<親 2>）
- G1（base=<PRE_FEATURE 10 桁>）: 未記帳 0 件・違反なし・rc=0
- G1 単体テスト 5 本: boundaries 11・gate 18・history 10・ledger 27・real_ledger 4 OK
- 実行計画一式 19 本: 存在・設計検査済み
## 未完了
- （無ければ「なし」）
## 要裁定
- （無ければ「なし」）
```

---

### Task 4: moores-code-review で全ブランチレビュー（必須）

**Files:**
- Modify: レビューの自動適用が触ったファイルだけ

**Interfaces:**
- Consumes: Task 1〜3 のコミット一式（`origin/feature/uitk..HEAD`）
- Produces: レビュー記録 `../moorestech_logs/harness/` 配下（`$RUNDIR`）

- [ ] **Step 1: 必ず moores-code-review スキルで全ブランチレビューを実行する（自動実行・ゴール文言による省略不可）**

対象は `origin/feature/uitk...HEAD`。Codex ホストでは `.agents/skills/moores-code-review/references/codex-host.md` の手順で `scripts/codex_workflow_runner/run.mjs` を使う。台帳の解決は Task 2 Step 4 の期待値が正で、レビューが別の解き方を提案しても Requirements 3〜6 と食い違うものは採らず、`done.md` の「要裁定」に書く

- [ ] **Step 2: 自動適用があれば Task 3 Step 1・2 を回し直し、コミットする**

Run: Task 3 Step 1・2 と同じコマンド
Expected: 同じ期待値。続けて:
```bash
git add -A docs .github
git commit -m "fix(uitk): W0-merge の moores-code-review 反映

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
（自動適用が無ければコミットしない）

---

### Task 5: feature/uitk への PR と取り込み（必須）

**Files:**
- なし（PR・マージ・後片付け）

**Interfaces:**
- Consumes: Task 4 後の HEAD
- Produces: `origin/feature/uitk` のマージコミット（W0-switch・W0-skill・W0-tools・L0-* が `--from origin/feature/uitk` で起こす土台）

- [ ] **Step 1: pr-create スキルで `base=feature/uitk` の PR を作る**

push して PR を作る（`gh pr create --base feature/uitk`）。PR 本文の先頭行は報告ページ `# https://review.moores.tech/pr/<番号>/report/` だけ。報告ページは `~/hermes-agent/data/services/pr-review/publish/README.md` の手順で、manifest は次の形（文書だけの変更なので `media` は空）:
```json
{
  "head": "<HEAD 10 桁>",
  "summary": ["origin/master（<MERGE_TARGET 10 桁>）を feature/uitk へ取り込み、台帳 docs/uitk/TODO.md の束 1 を暗転へ戻した", "束 1 の 3 行（pauseMenu・settings・toast）の所有者を Web に、未記帳 2 件を記帳した", "UITK 移行の実行計画一式（execution/ と詳細計画 12 本）を入れた"],
  "media": [],
  "no_media_reason": "文書と台帳だけの取り込みで、画面・挙動の変更が無い",
  "verification": ["G1（ローカル）: 束 1 未記帳 0 件・違反なし", "G1 単体テスト 5 本 OK（11/18/10/27/4）"]
}
```
`python3 /Users/sakastudio/hermes-agent/data/services/pr-review/publish/publish_pr_report.py <PR番号> <manifest.json>` が失敗したら、報告 URL 無しで完了にしない

- [ ] **Step 2: feature/uitk の進みを取り込み、G1 緑を見る**

Run:
```bash
git fetch origin
git merge --no-ff --no-edit origin/feature/uitk
git push
gh pr checks <PR番号> --watch=false
```
Expected: `origin/feature/uitk` が PR 作成後に進んでいなければ `Already up to date.`。進んでいて衝突したら、`docs/uitk/TODO.md` の「## 追従記録」は両方の行を採り、ほかは止めて指揮に報告する。進んでいたら Task 3 Step 1・2 を `--base origin/feature/uitk` で回し直す。`gh pr checks` で `UITK Web follow gate (G1)` と `UITK Web follow gate self-test` が `pass`。G1 のジョブログに `G1: 束 1（暗転・起点 979725bcd1）: 未記帳 0 件` があることを `gh run view --log` で確かめる（G1 は feature/uitk の中では未記帳を赤にしないので、件数を目で見る）。checks が終わっていなければ前景で `gh pr checks <PR番号> --watch` を実行して待つ

- [ ] **Step 3: マージコミットで取り込む**

Run: `gh pr merge <PR番号> --merge`
Expected: マージされ、`git fetch origin && git log -1 --format='%H %P' origin/feature/uitk` の親が 2 つ

- [ ] **Step 4: 後片付けと記録**

Run:
```bash
bd close moorestech-albof.20 --reason "W0-merge: PR #<番号> で master <MERGE_TARGET 10 桁> を feature/uitk へ取り込み、束 1 を暗転へ戻した"
```
`done.md` の証拠に PR 番号・マージコミット・G1 のジョブ URL を足す。その後、指揮が `moores-wt rm <w0-merge の worktree 名>` を実行できるよう、未コミット・未 push が無いことを `git status --short`（空）と `git log origin/uitk-lane/w0-merge..HEAD`（空）で確かめる。セッション終了可能状態（全作業がコミット・push 済みで PR がマージ済み）を確認して終える

---

## Self-Review

1. Requirements coverage: R1・R2 → Task 1 Step 2・Task 2 Step 1/5、R3〜R6 → Task 2 Step 2〜4、R7 → Task 1 Step 3〜4、R8・R9 → Task 3、R10 → Task 5。漏れなし
2. Placeholder scan: `<番号>`・`<HEAD 10 桁>` 等は実行時にしか決まらない値（PR 番号・SHA）で、取り方を同じステップに書いた。「TBD」「適切に」は無い
3. Type consistency: `target.env` の `PRE_FEATURE`・`MERGE_TARGET` を Task 1 で定義し Task 2・3・5 で同名で読む。`FAILURE_TOAST_ROW` の文は Task 2 Step 2 と Step 4 の期待値で一致
4. 保留・縮退経路: スクリプトの `fail()`・Task 1 の `MISSING/UNCHECKED`・Task 2 Step 1 の想定外衝突は、どれも書き換えずに止めて指揮へ理由を報告する（黙って続行しない）。解消の条件は「指揮が前提（docs ブランチの push・計画のレビュー・台帳の形）を直してレーンを再起動する」。取り込み先の固定（R2）は残りの master を I-weekly が取り込むことで解消する（I-weekly は実行計画 §3 の常設作業）
5. 「決定的」を根拠にした選択: 取り込み先の固定先 `cb4b23e9d8` は「Web 変更 0・W0-cef 無し」を 2026-11-06 に実測した値で、決定性ではなく G1 の未記帳 0 を保証する根拠で選んだ
6. 呼び出し側への責務漏出: 新設サービス・API は無い（該当なし）

2026-11-06 に、この Task 2 のスクリプトと Task 3 の検査を、`origin/feature/uitk`（`54c8a4c8c8`）と `origin/master`（`cb4b23e9d8`）の ref 無しの模擬マージ（`4549eca83f`）で実際に回した: 解決後の衝突マーカー 0・`| UITK |` 0、G1 は `--base origin/feature/uitk`・`--base 979725bcd1`・`--base origin/master` の 3 通りとも「束 1 未記帳 0 件・違反なし・rc=0」、単体テスト 5 本 OK（11/18/10/27/4）

---

## 判断記録（ADR）

設計 ADR: 新規の `docs/adr/` は無い。従う裁定は Global Constraints の `.decisions/` 6 本。実行計画 `docs/uitk-migration/execution/execution-plan.md` の §1（W0-merge 行）・§3・§4.3 と、調査 `research/merge-delta.md` §4〜§6 を前提にした。

| ID | 判断 | 出所 | 棄却した案と理由 |
|---|---|---|---|
| M-1 | 衝突 1 は列ごとに混ぜる（所有者・toast の agent 既定は master 側、pauseMenu の実測の結論は feature/uitk 側の撮影 URL） | agent 既定（merge-delta §5 の推奨） | 全部 master 側: 段階 1 の UITK ポーズの実測（撮影 URL）を捨てる理由が無い／全部 feature 側: 所有者 `UITK` と束 `暗転` が台帳のパースで矛盾する（`ledger.py` が 3 行分落とす。merge-delta §5 の実測） |
| M-2 | 「通信失敗のトースト」行は消さず、全体スイッチ前提の文に書き換え、結論を W0-switch に渡す | agent 既定 | そのまま残す: 前提（ポーズ中だけ Web 休止・toast 行の所有者が UITK）が両方消えて誤りになる／消す: W0-switch が確かめるべき論点（UITK 側に通信失敗の経路が無いか）が台帳から落ちる |
| M-3 | `3e946e4bd3` は `写した`（写し先は同コミットの C# の文言）、`5421c7f9f0` は `捨てた`（W0-switch で旧 R1 の Web 側ごと外す） | agent 既定（実行計画 §1 の W0-merge 行） | `3e946e4bd3` を `捨てた`: Web 側は生成物の追随だが、文言そのものは UITK のトーストへ写っているので `写した` が事実に合う／`5421c7f9f0` を `写した`: 旧 R1 は全体スイッチの裁定で外す仕組みで、UITK 側に写すものが無い |
| M-4 | 取り込み先は実行時の `origin/master`。master に Web 変更か W0-cef が入っていたら `cb4b23e9d8` に固定し、残りは I-weekly | agent 既定（planning 中に新規） | 常に最新 master: Web 変更の `写した` は UITK 側の実装（I-follow）を伴い、W0-cef は Editor と cef-record §6 の解決表を要するので、Editor 不要の 1 PR に収まらない／`e95dc17db1` 固定: その後の `cb4b23e9d8`（prefab のみ）を取り込まない理由が無い |
| M-5 | 台帳の解決は使い捨てスクリプトで行い、コミットしない。判定（pauseMenu 行・衝突の塊）は 1 か所にまとめる（片方だけ直して食い違うのを防ぐ。設計検査の弱 1 件） | agent 既定 | 手で編集: pauseMenu 行は URL 4 本を含む 1 行 1,000 字超で、列の取り違えが起きやすい／スクリプトを repo に入れる: 一度きりの解決で再利用先が無い（YAGNI） |
| M-6 | 実行計画一式は `origin/docs/uitk-execution-plan` をマージして入れ、19 本の存在と 12 本の設計検査済みを機械で確かめてから入れる | agent 既定（実行計画 §5-5「W0 の 12 本は W0-merge に同梱」） | 別の docs PR: 実行計画 §1 が同じ PR と決めている／検査せず入れる: レビューフローを通っていない計画でレーンが起きる |
| M-7 | unityプレイ録画テストのタスクを入れない | agent 既定 | このレーンが書くのは台帳と文書だけで、ランタイム挙動に触れない。取り込みで入る master の `CleanRoom*.prefab` の寸法変更は master で既にマージ済みの変更で、このレーンの判定経路ではない |
| M-8 | 台帳「読み方と運用」末尾の R4 手順（`UiOwnershipEditorOverride.Set(...)`）はこのレーンでは消さない | agent 既定 | ここで消す: 手順の対象（所有表の Editor 上書き）はまだコードにあり、消すのは作り直しの W0-switch（stage1-asbuilt §3.1）。先に消すと W0-switch までの間、Editor での見比べ手順が台帳から失われる |
