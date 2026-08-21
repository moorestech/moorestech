# CI再設計 B: 日次ビルド失敗の無人修復パイプライン Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** Plan A が起票する「日次ビルド失敗」ラベル付き Issue を起点に、既存の PR ラベル駆動ステートマシン（`poller.py`）を Issue 起点へ拡張し、04:00〜09:00 JST の枠内で調査→前方修正→PR作成→ビルド検証ラベルによる緑確認までを無人で回す。

**Architecture:** 既存 poller の構造（ラベルが再開チェックポイントの正・`state/<key>/` のディスク状態と突き合わせて冪等に遷移・cmux ワークスペースで対話モード `claude` を起動）をそのまま踏襲し、`pr-<N>` と並ぶ第2の系統として `issue-<N>` を足す。修復処理自体は poller には持たせず、moorestech リポジトリ側の新規スキル `daily-build-repair` が担う（既存の `pr-independent-review` / `pr-adjudicated-apply` と同じ役割分担）。

**Tech Stack:** Python 3（`poller.py` / `unittest`）/ cmux CLI / `gh` CLI / Claude Code スキル（Markdown）/ launchd + supervisor（`services.json`）

## Requirements

設計対話（grill）で確定した要件。ADR: `docs/adr/0028-ci-build-strategy.md`

- **R1.** 「日次ビルド失敗」ラベル付きの open Issue が、poller の管理対象として拾われる。**受け入れ基準**: `fetch_open_issues()` が当該ラベルの Issue を返し、`dispatch_issue_label()` がラベルからフェーズを決める。ラベルが付いていない Issue は対象外。
- **R2.** 修復セッションは 04:00〜09:00 JST の枠でのみ**起動**される。**受け入れ基準**: 03:59 と 09:01 の擬似時刻で `handle_repair_waiting` を呼ぶと起動せず、04:00 と 08:59 では起動する。
- **R3.** 09:00 JST 到達時、実行中の修復セッションは途中経過を Issue へ残してから停止する。**受け入れ基準**: 枠外かつ実行中のとき、poller が停止指示プロンプトを同ペインへ送り、`repair-result.json` の出現か猶予時間の経過をもってワークスペースを閉じる。Issue はクローズせずラベルを待ちへ戻し、翌朝の枠で継続できる。
- **R4.** 修復は前方修正のみで、bisect は行わない。**受け入れ基準**: `daily-build-repair` スキルの手順に bisect が現れず、「Issue 本文の容疑者PR一覧と失敗ログを材料に現在の master を直す」と明記されている。
- **R5.** 修復エージェントは自分の PR に「ビルド検証」ラベルを付け、ビルドが緑になったことを確認してから完了扱いにする。**受け入れ基準**: スキル手順にラベル付与と `gh pr checks` 相当の緑待ちが含まれ、`repair-result.json` の `verified` が真のときだけ status が `success` になる。
- **R6.** 修復 PR は自動マージしない。**受け入れ基準**: スキル手順に `gh pr merge` が現れない。
- **R7.** 同時に走る修復セッションは1本まで。**受け入れ基準**: 実行中の修復が1本あるとき、2件目の Issue は起動されず待ちのまま据え置かれる。
- **R8.** 既存の PR レビュー系統の挙動を一切変えない。**受け入れ基準**: 変更後も `test_poller.py` の既存テストが全て通る。
- **R9.** Mac の再起動や poller の再起動を挟んでも、ラベルとディスク状態から続きから動く。**受け入れ基準**: `state/issue-<N>/` に既存の session/workspace が残っている状態で `handle_repair_running` を呼ぶと、新規起動せず生存確認へ進む。

**やらないこと（スコープ境界）:**

- **GitHub Actions 側は Plan A の担当。** 本 plan は「Issue が既に正しいラベルと本文で立っている」ことを前提にする。
- **裁定サイト（`pr-review-site`）への修復フェーズ表示は作らない。** 修復に人間の裁定ステップは無い（前方修正のみ）。
- **Discord 通知の新設はしない。** 既存の `hermes` 通知経路は失敗時の人呼び出しにのみ流用する。
- **修復が失敗した場合の自動リトライは1回まで**（既存 `MAX_APPLY_RETRY` と同じ思想）。無限に粘らせない。

## Global Constraints

- **poller は git 管理下に無い。** `~/hermes-agent/data/services/pr-review/` で直接編集する。編集前に必ず `cp poller.py poller.py.bak-$(date +%Y%m%d-%H%M%S)` でバックアップを取る（既存の `.bak-20260821-headdrift` と同じ流儀）。
- **`services.json` はループ毎（5秒）に再読込される。** サービス定義の変更に supervisor の再起動は不要。
- **`poller.py` の実行は periodic 120 秒。** 長時間ブロックする処理を同期で書かない（メインループが他サービスごと止まる）。
- **テストは `unittest`。** 既存 `test_poller.py` は `unittest.TestCase` のクラス群で構成されている。pytest 記法を混ぜない。
- **ラベル名は逐語一致させる。** 本 plan で使うのは `日次ビルド失敗` / `日次ビルド修復:実行中` / `日次ビルド修復:PR作成済` / `日次ビルド修復:失敗` の4つ。Plan A が作るのは最初の1つのみで、残り3つは本 plan の Task 5 で作成する。
- **スキルの git 正本は `.agents/skills/` のみ。** `.claude/skills` と `.codex/skills` はそこへの symlink なので、実体を複製しない。
- **秘密情報を書かない。** `.env` や `config.yaml` の値をスキル・plan・ログに転記しない。

---

### Task 1: `daily-build-repair` スキルを作る

修復処理の本体。poller はこのスキルを起動するだけで、修復ロジックは持たない（既存の `pr-independent-review` / `pr-adjudicated-apply` と同じ役割分担）。

**Files:**
- Create: `.agents/skills/daily-build-repair/SKILL.md`

**Interfaces:**
- Consumes: 「日次ビルド失敗」ラベル付き Issue。本文には Plan A が埋め込んだ `<!-- daily-build-issue -->` マーカー、`前回グリーン: <sha>`、`## 容疑者PR:` 見出しの箇条書き、`## 失敗ジョブ:` 見出しとログURLが含まれる。
- Produces: `repair-result.json` を **`rundir` 相当のディレクトリ**（`$PR_REVIEW_RUNDIR_BASE/issue-<N>/repair-result.json`）へ書く。既存の `findings.json` / `apply-result.json` が `rundir(number)`（既定 `~/hermes-agent/data/repos/moorestech_logs/harness/pr-independent-review/runs/pr-<N>/`）に置かれる前例に合わせる。**`state/` は poller の内部状態専用で、成果物は置かない。** スキーマ:
  ```json
  {"status": "success|failure|timeout", "pr_number": 1234, "verified": true, "summary": "...", "remaining": "..."}
  ```
  `status: "success"` は `verified: true`（ビルド検証ラベルで緑を確認済み）のときだけ許される。`timeout` のときは `remaining` に残作業を書く。poller（Task 3/4）はこのファイルの出現をフェーズ完了の合図にする。
  スキルは環境変数 `PR_REVIEW_RUNDIR_BASE`（既定値は上記）を読んで書き先を決める。

- [ ] **Step 1: 既存スキルの構成を確認する**

Run:
```bash
head -30 .agents/skills/pr-adjudicated-apply/SKILL.md
ls .agents/skills/pr-adjudicated-apply/
```
Expected: frontmatter（`name` / `description`）を持つ Markdown。出力契約（`apply-result.json`）が Step として明記されている。

- [ ] **Step 2: スキルを作成する**

Create `.agents/skills/daily-build-repair/SKILL.md`。以下を必ず含める:

- **frontmatter**: `name: daily-build-repair`、`description` に「日次ビルド失敗Issueを起点に前方修正しPRを作る無人スキル。Use When: `/daily-build-repair <Issue番号>` で起動された時」を書く。
- **Step 1 — 対象の読み取り**: `gh issue view <N> --json title,body,labels` で Issue を読む。本文の `## 失敗ジョブ:` のログURLを `gh run view <run-id> --log-failed` で辿り、実際のエラー行を取得する。
- **Step 2 — 作業場所の用意**: `moores-wt new fix/daily-build-<N> --no-editor` で使い捨て worktree を作る。**メインワークツリーで作業しない**（`CLAUDE.local.md` の裁定）。
- **Step 3 — 前方修正**: 現在の master に対して直す。**bisect は行わない**。`## 容疑者PR:` の一覧は「どのPRの変更が怪しいか当たりを付ける材料」であって、そのPRを revert するためのものではない。エラーが指すファイルを直接直すのが基本。
- **Step 4 — ローカル検証**: `.cs` を触ったら `uloop compile` を通す。関連するテストがあれば走らせる。
- **Step 5 — PR作成**: `pr-create` スキルで PR を作る。本文に `Fixes #<N>` と、何が壊れていて何を直したかを書く。**`gh pr merge` は絶対に実行しない。**
- **Step 6 — ビルド検証**: 作成した PR に `gh pr edit <PR> --add-label "ビルド検証"` を付け、`Unity Build` の結果を待つ。緑なら `verified: true`。
- **Step 7 — 結果の書き出し**: `repair-result.json` を上記スキーマで書く。**このファイルを書くまでセッションを終えない。**
- **Step 8 — 停止指示を受けたとき**: poller から停止指示（`## 停止指示` を含むプロンプト）を受けたら、その時点で「どこまで調べたか・何を直しかけたか・残作業」を `gh issue comment <N>` で Issue へ投稿し、`repair-result.json` に `status: "timeout"` と `remaining` を書いてから終了する。

さらに **HARD GATE** として次を明記する:

> 修復対象は日次ビルドを赤くしている原因のみ。ついでのリファクタ・無関係な改善・他の不具合修正は一切行わない。見つけた別の問題は Issue へコメントで残すだけにする。

- [ ] **Step 3: symlink 経由でスキルが見えることを確認する**

Run:
```bash
ls -l .claude/skills | head -3
ls .claude/skills/daily-build-repair/SKILL.md
```
Expected: `.claude/skills` が `.agents/skills` への symlink で、新スキルがそこから見える。**symlink の実体を複製してはならない。**

- [ ] **Step 4: frontmatter が妥当か確認する**

Run:
```bash
python3 -c "
import re
src = open('.agents/skills/daily-build-repair/SKILL.md').read()
m = re.match(r'^---\n(.*?)\n---\n', src, re.S)
assert m, 'frontmatter がない'
import yaml
fm = yaml.safe_load(m.group(1))
assert fm['name'] == 'daily-build-repair', fm
assert 'description' in fm
assert 'gh pr merge' not in src, '自動マージが手順に含まれている'
assert 'bisect' not in src.replace('bisect は行わない', '').replace('bisectは行わない', ''), 'bisect手順が残っている'
print('ok')
"
```
Expected: `ok`

- [ ] **Step 5: コミットする**

```bash
git add .agents/skills/daily-build-repair/SKILL.md
git commit -m "feat(skill): 日次ビルド失敗Issueから前方修正しPRを作るdaily-build-repairスキルを追加"
```

---

### Task 2: poller に Issue 系統のフェッチとラベル振り分けを足す

**Files:**
- Modify: `~/hermes-agent/data/services/pr-review/poller.py`
- Modify: `~/hermes-agent/data/services/pr-review/test_poller.py`

**Interfaces:**
- Consumes: なし
- Produces:
  - 定数 `LABEL_REPAIR_WAITING = "日次ビルド失敗"` / `LABEL_REPAIR_RUNNING = "日次ビルド修復:実行中"` / `LABEL_REPAIR_DONE = "日次ビルド修復:PR作成済"` / `LABEL_REPAIR_FAILED = "日次ビルド修復:失敗"`
  - `REPAIR_LABEL_PRIORITY: list[str]` — 上記を実行中→待ちの順に並べた優先順位リスト
  - `fetch_open_issues() -> list[dict]` — `gh_read_json` で `gh issue list` を叩き、各要素が `number` / `title` / `labels` を持つリストを返す
  - `dispatch_issue_label(issue: dict) -> str | None` — 管理対象なら該当ラベル、対象外なら `None`
  - `issue_state_dir(number: int) -> str` — `os.path.join(STATE_DIR, f"issue-{number}")`（既存 `pr_state_dir` の Issue 版）
  - `issue_rundir(number: int) -> str` — `os.path.join(RUNDIR_BASE, f"issue-{number}")`（既存 `rundir` の Issue 版）
  - 既存ヘルパ `gh_edit_labels(number, add, remove, description)` と `gh_comment(number, body, description)` に **`kind: str = "pr"` 引数を追加**して `gh pr` / `gh issue` を出し分ける。既定値が `"pr"` なので既存の呼び出し側は一切変更しない
  - 既存ヘルパ `has_marker(number, name)` / `set_marker(number, name)` にも同様に `kind: str = "pr"` を追加し、marker の置き先を `pr_state_dir` / `issue_state_dir` で切り替える

- [ ] **Step 1: バックアップを取る**

Run:
```bash
cd ~/hermes-agent/data/services/pr-review
cp poller.py "poller.py.bak-$(date +%Y%m%d-%H%M%S)"
cp test_poller.py "test_poller.py.bak-$(date +%Y%m%d-%H%M%S)"
ls -la poller.py.bak-* | tail -2
```
Expected: 新しい `.bak-` ファイルが2つできている。

- [ ] **Step 2: 既存テストが今すべて通ることを記録する**

Run:
```bash
cd ~/hermes-agent/data/services/pr-review && /opt/homebrew/bin/python3 -m unittest test_poller 2>&1 | tail -5
```
Expected: `OK`（件数を控えておく。Task 6 でこの件数を下回っていないことを確認する）

- [ ] **Step 3: 失敗するテストを書く**

`test_poller.py` の末尾に追加:

```python
class IssueDispatchTest(unittest.TestCase):
    def test_repair_label_is_dispatched(self):
        issue = {"number": 42, "labels": [{"name": poller.LABEL_REPAIR_WAITING}]}
        self.assertEqual(poller.dispatch_issue_label(issue), poller.LABEL_REPAIR_WAITING)

    def test_running_wins_over_waiting(self):
        issue = {
            "number": 42,
            "labels": [
                {"name": poller.LABEL_REPAIR_WAITING},
                {"name": poller.LABEL_REPAIR_RUNNING},
            ],
        }
        self.assertEqual(poller.dispatch_issue_label(issue), poller.LABEL_REPAIR_RUNNING)

    def test_unrelated_issue_is_ignored(self):
        issue = {"number": 43, "labels": [{"name": "bug"}]}
        self.assertIsNone(poller.dispatch_issue_label(issue))

    def test_state_dir_is_namespaced(self):
        self.assertTrue(poller.issue_state_dir(42).endswith("issue-42"))
        self.assertNotIn("pr-42", poller.issue_state_dir(42))
```

- [ ] **Step 4: テストを実行して失敗を確認する**

Run:
```bash
cd ~/hermes-agent/data/services/pr-review && /opt/homebrew/bin/python3 -m unittest test_poller.IssueDispatchTest -v 2>&1 | tail -10
```
Expected: FAIL — `AttributeError: module 'poller' has no attribute 'LABEL_REPAIR_WAITING'`

- [ ] **Step 5: 定数と関数を実装する**

`poller.py` の既存ラベル定数（`LABEL_DONE = "独立レビュー&対応完了"` の直後）に追加:

```python
# 日次ビルド失敗Issueの修復系統。PRレビュー系統とは独立したラベル空間を使う（ADR 0028）
# Labels for the daily build repair track; a label space independent from the PR review track (ADR 0028)
LABEL_REPAIR_WAITING = "日次ビルド失敗"
LABEL_REPAIR_RUNNING = "日次ビルド修復:実行中"
LABEL_REPAIR_DONE = "日次ビルド修復:PR作成済"
LABEL_REPAIR_FAILED = "日次ビルド修復:失敗"

REPAIR_LABEL_PRIORITY = [
    LABEL_REPAIR_RUNNING,
    LABEL_REPAIR_WAITING,
]
```

`LABEL_PRIORITY` を使う既存の `dispatch_label` の直後に、Issue 版を追加:

```python
def dispatch_issue_label(issue: dict) -> str | None:
    names = {label["name"] for label in issue.get("labels", [])}
    for label in REPAIR_LABEL_PRIORITY:
        if label in names:
            return label
    return None


def issue_state_dir(number: int) -> str:
    return os.path.join(STATE_DIR, f"issue-{number}")


def issue_rundir(number: int) -> str:
    return os.path.join(RUNDIR_BASE, f"issue-{number}")


def fetch_open_issues() -> list[dict]:
    # 外部境界: gh CLIの呼び出し。既存のfetch_open_prsと同じgh_read_jsonを通すので例外の形も揃う
    # External boundary: the gh CLI call; going through the same gh_read_json as fetch_open_prs keeps failures uniform
    return gh_read_json([
        "issue", "list",
        "--repo", REPO,
        "--state", "open",
        "--label", LABEL_REPAIR_WAITING,
        "--json", "number,title,labels",
        "--limit", "20",
    ])
```

> `fetch_open_prs`（`poller.py:746`）の実装を読み、同じヘルパ・同じ例外の投げ方に揃えること。

- [ ] **Step 5.5: 既存ヘルパに `kind` 引数を足して Issue でも使えるようにする**

`gh_edit_labels`（`poller.py:329`）/ `gh_comment`（`:338`）/ `has_marker`（`:215`）/ `set_marker`（`:219`）に `kind: str = "pr"` を追加する。**既定値が `"pr"` なので既存の呼び出し側は1行も変えない。**

```python
def gh_edit_labels(number: int, add: list[str], remove: list[str], description: str,
                   kind: str = "pr") -> None:
    # PRとIssueでサブコマンドだけが違う。既定値prにより既存の呼び出しは挙動不変
    # Only the subcommand differs between PRs and issues; the "pr" default keeps existing callers unchanged
    args = [kind, "edit", str(number), "--repo", REPO]
    ...


def has_marker(number: int, name: str, kind: str = "pr") -> bool:
    base = pr_state_dir(number) if kind == "pr" else issue_state_dir(number)
    return os.path.exists(os.path.join(base, f"marker.{name}"))
```

`set_marker` も同様に `base` の出し分けを入れる。`gh_comment` も `args = [kind, "comment", ...]` に変える。

**既存呼び出しの挙動が変わっていないことを、既存テスト全通過（Step 7）で確認する。**

- [ ] **Step 6: テストを実行して通ることを確認する**

Run:
```bash
cd ~/hermes-agent/data/services/pr-review && /opt/homebrew/bin/python3 -m unittest test_poller.IssueDispatchTest -v 2>&1 | tail -5
```
Expected: `OK`（4 tests）

- [ ] **Step 7: 既存テストが壊れていないことを確認する**

Run:
```bash
cd ~/hermes-agent/data/services/pr-review && /opt/homebrew/bin/python3 -m unittest test_poller 2>&1 | tail -3
```
Expected: `OK`（Step 2 で控えた件数 + 4）

---

### Task 3: 深夜枠ゲート付きで修復セッションを起動する

**Files:**
- Modify: `~/hermes-agent/data/services/pr-review/poller.py`
- Modify: `~/hermes-agent/data/services/pr-review/test_poller.py`

**Interfaces:**
- Consumes: `LABEL_REPAIR_WAITING`, `LABEL_REPAIR_RUNNING`, `issue_state_dir`（Task 2）
- Produces:
  - `REPAIR_WINDOW_START_HOUR = 4` / `REPAIR_WINDOW_END_HOUR = 9`（JST の時）
  - `MAX_CONCURRENT_REPAIRS = 1`
  - `in_repair_window(now: datetime) -> bool` — 引数の `now` は JST の naive datetime。**テスト可能にするため必ず引数で受ける**（内部で `datetime.now()` を呼ばない）
  - `handle_repair_waiting(issue: dict, now: datetime, repair_budget: list[int]) -> None`

- [ ] **Step 1: 失敗するテストを書く**

`test_poller.py` の末尾に追加:

```python
import datetime


class RepairWindowTest(unittest.TestCase):
    def test_before_window_is_closed(self):
        self.assertFalse(poller.in_repair_window(datetime.datetime(2026, 8, 22, 3, 59)))

    def test_window_start_is_open(self):
        self.assertTrue(poller.in_repair_window(datetime.datetime(2026, 8, 22, 4, 0)))

    def test_last_minute_is_open(self):
        self.assertTrue(poller.in_repair_window(datetime.datetime(2026, 8, 22, 8, 59)))

    def test_window_end_is_closed(self):
        self.assertFalse(poller.in_repair_window(datetime.datetime(2026, 8, 22, 9, 0)))

    def test_midday_is_closed(self):
        self.assertFalse(poller.in_repair_window(datetime.datetime(2026, 8, 22, 14, 0)))
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run:
```bash
cd ~/hermes-agent/data/services/pr-review && /opt/homebrew/bin/python3 -m unittest test_poller.RepairWindowTest -v 2>&1 | tail -5
```
Expected: FAIL — `module 'poller' has no attribute 'in_repair_window'`

- [ ] **Step 3: 枠判定を実装する**

`poller.py` の定数群（`MAX_CONCURRENT_REVIEWS = 2` の近く）に追加:

```python
# 修復は深夜枠でのみ起動する。昼間のPRレビュー2本枠・人間の作業・Unity Editorと食い合わせない（ADR 0028）
# Repairs only start in the night window so they never contend with the daytime review budget,
# the human's work, or the Unity Editors (ADR 0028)
REPAIR_WINDOW_START_HOUR = 4
REPAIR_WINDOW_END_HOUR = 9
MAX_CONCURRENT_REPAIRS = 1
REPAIR_STOP_GRACE_SECONDS = 600
```

関数を追加:

```python
def in_repair_window(now: datetime) -> bool:
    return REPAIR_WINDOW_START_HOUR <= now.hour < REPAIR_WINDOW_END_HOUR
```

`poller.py` は既に `from datetime import datetime, timezone`（`poller.py:25`）を持っているので、**import の追加は不要**。型注釈は `datetime.datetime` ではなく `datetime` と書くこと（`import datetime` 形式ではないため）。
テスト側（`test_poller.py`）にだけ `import datetime` を追加し、テストコードでは `datetime.datetime(2026, 8, 22, 4, 0)` の形で構築する。

- [ ] **Step 4: テストを実行して通ることを確認する**

Run:
```bash
cd ~/hermes-agent/data/services/pr-review && /opt/homebrew/bin/python3 -m unittest test_poller.RepairWindowTest -v 2>&1 | tail -3
```
Expected: `OK`（5 tests）

- [ ] **Step 5: 起動ハンドラの失敗するテストを書く**

`test_poller.py` の末尾に追加。既存の `LaunchClaudeTest` / `ReviewConcurrencyCapTest` のモック手法をそのまま踏襲すること（`unittest.mock.patch` で `cmux` と `run_gh` を差し替える形）。

```python
class RepairWaitingTest(unittest.TestCase):
    def test_outside_window_does_not_launch(self):
        issue = {"number": 42, "labels": [{"name": poller.LABEL_REPAIR_WAITING}]}
        budget = [poller.MAX_CONCURRENT_REPAIRS]
        with unittest.mock.patch.object(poller, "launch_repair_session") as launch:
            poller.handle_repair_waiting(issue, datetime.datetime(2026, 8, 22, 14, 0), budget)
        launch.assert_not_called()

    def test_budget_exhausted_does_not_launch(self):
        issue = {"number": 42, "labels": [{"name": poller.LABEL_REPAIR_WAITING}]}
        budget = [0]
        with unittest.mock.patch.object(poller, "launch_repair_session") as launch:
            poller.handle_repair_waiting(issue, datetime.datetime(2026, 8, 22, 5, 0), budget)
        launch.assert_not_called()

    def test_in_window_with_budget_launches(self):
        issue = {"number": 42, "labels": [{"name": poller.LABEL_REPAIR_WAITING}]}
        budget = [1]
        with unittest.mock.patch.object(poller, "launch_repair_session") as launch:
            poller.handle_repair_waiting(issue, datetime.datetime(2026, 8, 22, 5, 0), budget)
        launch.assert_called_once()
        self.assertEqual(budget[0], 0)
```

- [ ] **Step 6: テストを実行して失敗を確認する**

Run:
```bash
cd ~/hermes-agent/data/services/pr-review && /opt/homebrew/bin/python3 -m unittest test_poller.RepairWaitingTest -v 2>&1 | tail -5
```
Expected: FAIL — `handle_repair_waiting` が存在しない

- [ ] **Step 7: 起動ハンドラを実装する**

既存の `handle_waiting` を手本にする（ラベル付替え → 開始コメント → cmux ワークスペース作成 → state ファイル書き出し、の順序と marker による重複コメント防止）。

```python
def handle_repair_waiting(issue: dict, now: datetime, repair_budget: list[int]) -> None:
    number = issue["number"]

    if not in_repair_window(now):
        log(f"issue-{number}: 深夜枠外のため起動見送り (outside repair window {now.hour}:00)")
        return

    if repair_budget[0] <= 0:
        log(f"issue-{number}: 同時実行上限のため起動見送り (repair budget exhausted)")
        return

    if not cmux.ping():
        log(f"issue-{number}: cmux応答なしのため起動見送り (cmux ping failed)")
        return

    launch_repair_session(issue)
    repair_budget[0] -= 1
```

`launch_repair_session` は既存 `handle_waiting`（`poller.py:769`）のレビュー起動処理と同形で書く。**起動は既存の `launch_claude` を通す**（cmux ワークスペースの生成・pid ファイル・DRYRUN ガードがそこに集約されているため、`cmux.create_workspace` を直接呼ばない）:

```python
def launch_repair_session(issue: dict) -> None:
    number = issue["number"]
    os.makedirs(issue_state_dir(number), exist_ok=True)
    os.makedirs(issue_rundir(number), exist_ok=True)

    gh_edit_labels(number, add=[LABEL_REPAIR_RUNNING], remove=[LABEL_REPAIR_WAITING],
                   description="修復開始", kind="issue")
    if not has_marker(number, "repair_started_comment", kind="issue"):
        gh_comment(number, "日次ビルド失敗の無人修復を開始します（前方修正のみ・bisectなし）。",
                   description="修復開始コメント", kind="issue")
        set_marker(number, "repair_started_comment", kind="issue")

    session = str(uuid.uuid4())
    prompt = f"{UNATTENDED_PREFACE}/daily-build-repair {number}"
    launched = launch_claude(number, prompt, "repair.pid", CLONE_DIR, session)

    write_text(os.path.join(issue_state_dir(number), "repair.started"), str(int(time.time())))
    write_text(os.path.join(issue_state_dir(number), "repair.retry"), "0")
    log(f"issue-{number}: 修復セッション起動 launched={launched} (session {session})")
```

> **`launch_claude`（`poller.py:422`）は `session_id_path(number, pid_name)` / `workspace_path(number, pid_name)` を通じて PR 前提のパスへ書く。** 実装を読み、これらにも `kind: str = "pr"` を足して `issue_state_dir` へ出し分ける（Task 2 Step 5.5 と同じ形）。`launch_claude` 自身にも `kind` を通す。既定値 `"pr"` で既存の呼び出しは挙動不変にすること。
>
> `cmux` は poller 内でのモジュール別名（テストが `patch.object(poller.cmux, ...)` で差し替えている）。`cmux_launcher` という名前で参照しない。

- [ ] **Step 8: テストを実行して通ることを確認する**

Run:
```bash
cd ~/hermes-agent/data/services/pr-review && /opt/homebrew/bin/python3 -m unittest test_poller.RepairWaitingTest -v 2>&1 | tail -3
```
Expected: `OK`（3 tests）

- [ ] **Step 9: 既存テストが壊れていないことを確認する**

Run:
```bash
cd ~/hermes-agent/data/services/pr-review && /opt/homebrew/bin/python3 -m unittest test_poller 2>&1 | tail -3
```
Expected: `OK`

---

### Task 4: 実行中の監視・完了判定・09:00の停止処理

**Files:**
- Modify: `~/hermes-agent/data/services/pr-review/poller.py`
- Modify: `~/hermes-agent/data/services/pr-review/test_poller.py`

**Interfaces:**
- Consumes: `launch_repair_session`, `issue_state_dir`, `in_repair_window`, `REPAIR_STOP_GRACE_SECONDS`（Task 3）
- Produces: `handle_repair_running(issue: dict, now: datetime) -> bool` — 戻り値は「このtickで飛行中と確定したか」。既存 `handle_running` の戻り値の意味と揃える。
- Produces: `REPAIR_STOP_PROMPT: str` — `## 停止指示` を含む本文。Task 1 のスキル Step 8 がこの見出しで停止を認識する。

- [ ] **Step 1: 失敗するテストを書く**

既存 `ReviewRunningTest`（`test_poller.py:257`）の `setUp` をそのまま踏襲する（`tempfile` で `STATE_DIR` / `RUNDIR_BASE` を差し替え、`poller.cmux` と `poller.probe` を patch する形）。

```python
class RepairRunningTest(unittest.TestCase):
    """修復セッションの分岐: result→完了/失敗/待ちへ戻す / 枠外→停止指示→猶予後クローズ / 枠内生存→継続"""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        poller.STATE_DIR = self.tmp.name
        poller.RUNDIR_BASE = os.path.join(self.tmp.name, "runs")
        os.makedirs(poller.issue_rundir(42), exist_ok=True)
        os.makedirs(poller.issue_state_dir(42), exist_ok=True)
        poller.DRYRUN = False
        poller.write_text(poller.session_id_path(42, "repair.pid", kind="issue"), "sid-42")
        poller.write_text(poller.workspace_path(42, "repair.pid", kind="issue"),
                          "02A4A452-C3FD-4781-8AE6-62DF315A1AB9")
        poller.write_text(os.path.join(poller.issue_state_dir(42), "repair.started"),
                          str(time.time() - 60))
        poller.write_text(os.path.join(poller.issue_state_dir(42), "repair.retry"), "0")
        self.issue = {"number": 42, "labels": [{"name": poller.LABEL_REPAIR_RUNNING}]}
        self.in_window = datetime.datetime(2026, 8, 22, 5, 0)
        self.out_window = datetime.datetime(2026, 8, 22, 9, 30)
        self.patches = {
            "labels": patch.object(poller, "gh_edit_labels"),
            "comment": patch.object(poller, "gh_comment"),
            "send": patch.object(poller.cmux, "send_text", return_value=True),
            "close": patch.object(poller.cmux, "close_workspace", return_value=True),
            "capture": patch.object(poller.cmux, "capture_tail", return_value=""),
            "alive": patch.object(poller, "session_alive", return_value=True),
            "last": patch.object(poller.probe, "last_assistant", return_value=("", 0.0)),
            "activity": patch.object(poller.probe, "last_activity_epoch", return_value=time.time()),
        }
        self.m = {k: p.start() for k, p in self.patches.items()}

    def tearDown(self):
        for p in self.patches.values():
            p.stop()
        self.tmp.cleanup()

    def _write_result(self, status, verified=True):
        path = os.path.join(poller.issue_rundir(42), "repair-result.json")
        with open(path, "w", encoding="utf-8") as f:
            json.dump({"status": status, "pr_number": 1234, "verified": verified,
                       "summary": "s", "remaining": ""}, f)

    def _added_labels(self):
        call = self.m["labels"].call_args
        return call.kwargs["add"] if "add" in call.kwargs else call.args[1]

    def test_result_success_moves_to_done(self):
        self._write_result("success", verified=True)
        self.assertFalse(poller.handle_repair_running(self.issue, self.in_window))
        self.assertIn(poller.LABEL_REPAIR_DONE, self._added_labels())
        self.m["close"].assert_called_once()

    def test_result_success_without_verified_is_failure(self):
        self._write_result("success", verified=False)
        self.assertFalse(poller.handle_repair_running(self.issue, self.in_window))
        self.assertIn(poller.LABEL_REPAIR_FAILED, self._added_labels())

    def test_result_failure_moves_to_failed(self):
        self._write_result("failure", verified=False)
        self.assertFalse(poller.handle_repair_running(self.issue, self.in_window))
        self.assertIn(poller.LABEL_REPAIR_FAILED, self._added_labels())

    def test_result_timeout_returns_to_waiting(self):
        self._write_result("timeout", verified=False)
        self.assertFalse(poller.handle_repair_running(self.issue, self.in_window))
        self.assertIn(poller.LABEL_REPAIR_WAITING, self._added_labels())

    def test_outside_window_sends_stop_prompt_once(self):
        poller.handle_repair_running(self.issue, self.out_window)
        self.m["send"].assert_called_once()
        self.assertIn("## 停止指示", self.m["send"].call_args.args[1])
        self.m["send"].reset_mock()
        poller.handle_repair_running(self.issue, self.out_window)
        self.m["send"].assert_not_called()

    def test_outside_window_after_grace_closes_workspace(self):
        poller.write_text(os.path.join(poller.issue_state_dir(42), "repair.stop_sent"),
                          str(int(time.time()) - poller.REPAIR_STOP_GRACE_SECONDS - 1))
        self.assertFalse(poller.handle_repair_running(self.issue, self.out_window))
        self.m["close"].assert_called_once()
        self.assertIn(poller.LABEL_REPAIR_WAITING, self._added_labels())

    def test_alive_in_window_stays_running(self):
        self.assertTrue(poller.handle_repair_running(self.issue, self.in_window))
        self.m["close"].assert_not_called()
        self.m["send"].assert_not_called()

    def test_existing_session_is_not_relaunched(self):
        with patch.object(poller, "launch_repair_session") as launch:
            self.assertTrue(poller.handle_repair_running(self.issue, self.in_window))
        launch.assert_not_called()
```

> 停止指示の送信済み判定は `state/issue-42/repair.stop_sent`（送信時刻のepochを書く）1本で行う。marker とは別系統にしないこと（送信時刻が猶予判定に必要なため、存在フラグだけでは足りない）。
>
> `_added_labels` が引数の位置とキーワードの両方に対応しているのは、`gh_edit_labels` の呼び出し形をここで決め打ちにしないため。実装側は既存 PR 側の呼び出しと同じ形に揃えること。

- [ ] **Step 2: テストを実行して失敗を確認する**

Run:
```bash
cd ~/hermes-agent/data/services/pr-review && /opt/homebrew/bin/python3 -m unittest test_poller.RepairRunningTest -v 2>&1 | tail -10
```
Expected: FAIL — `handle_repair_running` が存在しない

- [ ] **Step 3: 停止指示プロンプトを定義する**

`poller.py` の既存プロンプト定数群（`RESUME_PROMPT` の近く）に追加:

```python
# 09:00到達時に送る停止指示。スキル側(Step 8)がこの見出しで停止を認識し、途中経過をIssueへ残す
# Stop instruction sent at 09:00; the skill (Step 8) recognises this heading and posts its progress to the issue
REPAIR_STOP_PROMPT = (
    "## 停止指示\n"
    "深夜枠(04:00-09:00 JST)が終了しました。ここで作業を打ち切ってください。\n"
    "1. 「どこまで調べたか・何を直しかけたか・残作業」をIssueへ `gh issue comment` で投稿する\n"
    "2. repair-result.json に status=\"timeout\" と remaining を書く\n"
    "3. 新しい修正には着手しない\n"
)
```

- [ ] **Step 4: 監視ハンドラを実装する**

既存 `handle_running`（`poller.py:894`）を手本にする。観測点は既存と同じ3つ（`session_alive` による生死、`transcript_probe` による活動状況、成果物ファイルの出現）。

処理順:
1. `issue_rundir(number)/repair-result.json` が存在し `repair.started` 以降の mtime を持つなら、`status` に応じてラベルを付け替える。`success` かつ `verified: true` のときだけ `LABEL_REPAIR_DONE`、それ以外（`failure`、および `verified` が偽の `success`）は `LABEL_REPAIR_FAILED` へ付け替え、完了コメントを投稿し、ワークスペースを閉じて `False` を返す。
2. `status: "timeout"` の場合はラベルを `LABEL_REPAIR_WAITING` へ戻し（翌朝の枠で継続）、ワークスペースを閉じて `False` を返す。
3. 枠外（`not in_repair_window(now)`）かつ実行中なら、`state/issue-<N>/repair.stop_sent` が無ければ `REPAIR_STOP_PROMPT` を同ペインへ送り、送信時刻のepochを同ファイルへ書く。既にあり、そこから `REPAIR_STOP_GRACE_SECONDS` 経過していれば、ワークスペースを閉じてラベルを `LABEL_REPAIR_WAITING` へ戻す（**Issue はクローズしない**）。
4. プロセスが死んでいて result も無い場合、`repair.retry < 1` なら新セッションで再起動、以降は `LABEL_REPAIR_FAILED` にして人を呼ぶ（既存 `MAX_APPLY_RETRY` と同じ思想）。
5. session limit / weekly limit の扱いは既存 `handle_running` と同じ分岐をそのまま流用する。
6. 生存していれば `True` を返す。

- [ ] **Step 5: テストを実行して通ることを確認する**

Run:
```bash
cd ~/hermes-agent/data/services/pr-review && /opt/homebrew/bin/python3 -m unittest test_poller.RepairRunningTest -v 2>&1 | tail -5
```
Expected: `OK`（8 tests）

- [ ] **Step 6: `run_once` に Issue 系統を組み込む**

`run_once`（`poller.py:1414`）の PR 処理の後に、Issue 処理を追加する。**PR 側の処理を一切変更しない**こと。

```python
    # 修復系統(Issue)。PR系統とは独立した予算・独立したstateで回す
    # The repair track (issues) runs on its own budget and its own state, independent of the PR track
    now = datetime.now()
    try:
        issues = fetch_open_issues()
    except (RuntimeError, subprocess.TimeoutExpired, json.JSONDecodeError) as exc:
        log(f"gh issue list失敗、修復系統はこのtickをスキップ (gh issue list failed, skipping repair track): {exc}")
        return

    repair_targets = [(i, dispatch_issue_label(i)) for i in issues]
    repair_targets = [(i, label) for i, label in repair_targets if label is not None]
    in_flight_repairs = 0
    for issue, label in repair_targets:
        if label == LABEL_REPAIR_RUNNING:
            if handle_repair_running(issue, now):
                in_flight_repairs += 1

    repair_budget = [max(0, MAX_CONCURRENT_REPAIRS - in_flight_repairs)]
    for issue, label in repair_targets:
        if label == LABEL_REPAIR_WAITING:
            handle_repair_waiting(issue, now, repair_budget)
```

> **実行中を先に処理してから待ちを処理する**（既存 PR 側と同じ順序）。飛行中の本数を確定させてから予算を計算しないと、同一 tick で2本起動してしまう。

- [ ] **Step 7: 全テストが通ることを確認する**

Run:
```bash
cd ~/hermes-agent/data/services/pr-review && /opt/homebrew/bin/python3 -m unittest test_poller 2>&1 | tail -3
```
Expected: `OK`

- [ ] **Step 8: DRYRUN で1tick 空回しして例外が出ないことを確認する**

Run:
```bash
cd ~/hermes-agent/data/services/pr-review && PR_REVIEW_DRYRUN=1 /opt/homebrew/bin/python3 poller.py 2>&1 | tail -20
```
Expected: 例外なく終了する。ログに `管理対象PR` の行と、修復系統についての行（対象0件なら何も出ないか、その旨）が出る。

---

### Task 5: ラベル作成とデプロイ、実地スモークテスト

**Files:**
- Modify: `~/hermes-agent/data/services/pr-review/README.md`（状態遷移図に修復系統を追記）

**Interfaces:**
- Consumes: Task 1〜4 のすべて
- Produces: 稼働中の修復パイプライン

- [ ] **Step 1: 修復系統のラベルを作成する**

Run:
```bash
cd /Users/sakastudio/repos/moorestech
gh label create "日次ビルド修復:実行中" --description "無人修復セッションが走っている" --color "FBCA04" || true
gh label create "日次ビルド修復:PR作成済" --description "修復PRができてビルド緑を確認済み" --color "0E8A16" || true
gh label create "日次ビルド修復:失敗" --description "無人修復が失敗した。人の対応待ち" --color "B60205" || true
gh label list --limit 100 | grep "日次ビルド"
```
Expected: `日次ビルド失敗`（Plan A で作成済み）を含む4つのラベルが出る。

- [ ] **Step 2: README の状態遷移図に修復系統を追記する**

`~/hermes-agent/data/services/pr-review/README.md` に、既存の PR 状態遷移図の下へ修復系統の遷移図を追加する。

```
日次ビルド失敗（Plan Aのdaily-build-issue.cjsが起票）
    │  04:00-09:00 JSTの枠内 かつ 同時実行1本枠に空きがある場合のみ
    │  ラベル付替え → 開始コメント → cmux workspace "daily-build-repair <N>" で対話モードclaude起動
    ▼
日次ビルド修復:実行中 ──────────────────────────┐
    │ repair-result.json 検出                    │ 09:00到達: 停止指示を1回送る
    │  status=success & verified → PR作成済       │   → 猶予600秒でresultが出なければ
    │  status=failure            → 失敗           │      workspaceを閉じてラベルを待ちへ戻す
    │  status=timeout            → 待ちへ戻す      │ 死亡かつresult無し: retry<1で再起動、以降は失敗
    ▼
日次ビルド修復:PR作成済（人のレビュー待ち。自動マージはしない）
    │
    │ 日次が緑に戻ると daily-build-issue.cjs がIssueを自動クローズする
```

- [ ] **Step 3: supervisor が新しい poller を読み込んでいることを確認する**

`poller.py` は periodic サービスなので、ファイルを書き換えれば次の tick（120秒後）から新コードで走る。supervisor の再起動は不要。

Run:
```bash
sleep 130
tail -20 ~/hermes-agent/data/services/pr-review/state/poller.log
```
Expected: 直近2分以内のタイムスタンプで tick のログが出ており、例外のトレースバックが無い。

- [ ] **Step 4: 実地スモークテスト（枠外での不発火を確認）**

現在時刻が 04:00〜09:00 JST の**枠外**であることを確認したうえで、テスト用の Issue を立てる。

Run:
```bash
cd /Users/sakastudio/repos/moorestech
gh issue create --title "[smoke] 日次ビルド失敗のダミー" \
  --label "日次ビルド失敗" \
  --body $'<!-- daily-build-issue -->\n\nスモークテスト用のダミーIssue。\n\n## 失敗ジョブ:\n- （ダミー）\n\n## 容疑者PR:\n- （ダミー）'
sleep 130
grep "issue-" ~/hermes-agent/data/services/pr-review/state/poller.log | tail -5
```
Expected: `issue-<N>: 深夜枠外のため起動見送り (outside repair window ...)` のログが出て、**セッションは起動しない**。cmux にワークスペースが増えていないことも確認する。

- [ ] **Step 5: 実地スモークテスト（枠内での発火を確認）**

`REPAIR_WINDOW_START_HOUR` / `REPAIR_WINDOW_END_HOUR` を一時的に現在時刻を含む値へ書き換え、次の tick で起動することを確認する。

Run:
```bash
sleep 130
grep "issue-" ~/hermes-agent/data/services/pr-review/state/poller.log | tail -5
ls ~/hermes-agent/data/services/pr-review/state/issue-*/
```
Expected: `修復セッション起動 (launched repair session ...)` のログと、`state/issue-<N>/repair.session` などのファイルが出来ている。

確認後、**必ず時刻定数を 4 / 9 へ戻す**。

- [ ] **Step 6: スモークテストの後始末**

Run:
```bash
cd /Users/sakastudio/repos/moorestech
gh issue close <N> --reason "not planned"
rm -rf ~/hermes-agent/data/services/pr-review/state/issue-<N>
grep -n "REPAIR_WINDOW_START_HOUR\|REPAIR_WINDOW_END_HOUR" ~/hermes-agent/data/services/pr-review/poller.py
```
Expected: 定数が `4` と `9` に戻っている。起動してしまった cmux ワークスペースがあれば閉じる。修復エージェントが作った worktree があれば `moores-wt rm` で削除する。

- [ ] **Step 7: リポジトリ側の変更をコミットする**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/ci-build-strategy
git add .agents/skills/daily-build-repair/
git commit -m "docs(skill): daily-build-repairの手順をスモークテストの結果に合わせて調整"
```

> `poller.py` / `test_poller.py` / `README.md` は git 管理外なのでコミット対象に含まれない。**変更したことを PR 本文へ明記する**（レビュー時に差分が見えないため）。

---

### Task 6: 全ブランチレビュー（省略不可）

- [ ] **Step 1: moores-code-review スキルでブランチ全体をレビューする**

`moores-code-review` スキルを起動し、`master...feature/ci-build-strategy` の差分全体をレビューする。あわせて **git 管理外の `poller.py` の差分**（`diff poller.py.bak-* poller.py`）もレビュー対象として明示的に渡す。

**このタスクは自動実行であり、ゴール文言による省略はできない。**

- [ ] **Step 2: 指摘のうち機械的修正を適用し、設計判断は AskUserQuestion でユーザーへ諮る**

- [ ] **Step 3: 修正をコミットする**

```bash
git add -A
git commit -m "fix: レビュー指摘を反映"
```

---

## 判断記録（ADR）

設計セッションのADR: `docs/adr/0028-ci-build-strategy.md`
裁定の原本: `.decisions/2026-08-21-日次ビルド失敗は専用ラベルIssueから無人修復パイプラインを深夜に回す.md`、`.decisions/2026-08-22-無人修復の深夜枠は4時開始9時打ち切りとする.md`、`.decisions/2026-08-21-修復エージェントは前方修正のみで犯人特定はしない.md`

planning中に新たに生じた判断:

- **Issue 系統は PR 系統と独立したラベル空間・state 空間で回す**（`issue-<N>/` と `pr-<N>/`、`REPAIR_LABEL_PRIORITY` と `LABEL_PRIORITY`）。既存 PR レビュー系統の挙動を一切変えないことを最優先した。*出所: agent前提（要件 R8）*
- **修復ロジックは poller ではなくスキル側に置く**。既存の `pr-independent-review` / `pr-adjudicated-apply` と同じ役割分担で、poller は「起動・監視・ラベル遷移」だけを持つ（README に「poller自体はレビュー/apply処理を持たない」と明記されている前例に従う）。*出所: agent前提（前例一致）*
- **同時実行は1本**（`MAX_CONCURRENT_REPAIRS = 1`）。レビュー側の2本枠とは独立。深夜枠は Unity Editor もマシン資源も空いているが、修復は master を直す作業であり並行させる意味が薄い。*出所: agent前提*
- **`in_repair_window` は `now` を引数で受ける**。`datetime.now()` を内部で呼ぶとテストできないため。既存 poller にも同様の時刻依存があるが、新規部分はテスト可能な形にした。*出所: agent前提*
- **09:00 の停止は「指示を送る → 猶予600秒 → 強制クローズ」の2段**。即 kill だと途中経過が消え、裁定「途中経過をIssueに残して停止」を満たせない。猶予値600秒は agent 前提の初期値で、運用しながら調整する。*出所: agent前提（裁定は「途中経過を残す」までで、猶予秒数は未裁定）*
- **timeout 時はラベルを待ちへ戻す**。Issue をクローズせず待ちへ戻すことで、翌朝の枠で同じ Issue から継続できる。*出所: agent前提*
- **修復失敗のリトライは1回**。既存 `MAX_APPLY_RETRY = 1` と揃えた。*出所: agent前提（前例一致）*

## レイヤリング制約（配置と前例）

- **修復ロジックはスキル（`.agents/skills/daily-build-repair/`）に置き、poller は起動・監視・ラベル遷移だけを持つ。** 前例: README「poller自体はレビュー/apply処理を持たない」、`pr-independent-review` / `pr-adjudicated-apply` が同じ形。
- **成果物ファイルの出現をフェーズ完了の合図にする。** 前例: `findings.json`（レビュー）/ `apply-result.json`（apply）。`repair-result.json` は同じ役割・同じ場所（`state/<key>/`）に置く。
- **ラベルが再開チェックポイントの正で、ディスク状態と突き合わせて冪等に遷移する。** 前例: README 冒頭の設計方針。`state/issue-<N>/` は `state/pr-<N>/` と同じファイル構成（session / workspace / started / retry / marker.*）にする。
- **重複コメント防止は marker ファイル。** 前例: `marker.review_started_comment` 等。

**新規パターン（レビュー注目点）:**

1. **poller が Issue を扱うのは初めて。** `swap_labels` / `post_once` などの既存ヘルパが `gh pr` 決め打ちの場合、`kind` 引数での一般化が必要になる。**既存 PR 側の呼び出しの挙動が1ミリも変わらないこと**をレビューで重点的に見ること。
2. **時刻による起動ゲートは poller 初。** 既存の系統は時刻に依存せず常時動く。修復系統だけが枠を持つため、`run_once` 内で `datetime.now()` を1回だけ取って両ハンドラへ渡す形にしている（tick 内で時刻がずれないようにするため）。
3. **git 管理外ファイルの変更が PR 差分に現れない。** `poller.py` / `test_poller.py` / `README.md` の変更はレビュー時に見えないため、PR 本文へ差分を明記し、Task 6 で明示的にレビュー対象へ渡す運用でカバーする。
