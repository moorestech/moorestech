# repo-auto-pull の破棄可パス Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development（推奨）または superpowers:executing-plans を使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** always-on の `repo-auto-pull` が、Unityの自動生成dirtyとremoteの更新が同じファイルで衝突したときに恒久的に止まり続ける構造を断つ。

**Architecture:** repo ごとの設定に `disposable_paths` を持たせる。ff-only merge の直前に「未コミット変更 ∩ 取り込み予定の変更」を求め、それが**全部**リスト内なら破棄して続行、1件でもリスト外なら従来どおり blocked。破棄対象は必ずgit自身が報告したパスとの積集合なので、設定に何を書いてもリポジトリ外へは出られない。

**Tech Stack:** Python 3（標準ライブラリのみ）、`unittest`。対象は `~/hermes-agent/data/services/always-on/`（**gitリポジトリではない**ため本planにcommitステップは無い）。

## Requirements

1. 衝突dirtyが全部 `disposable_paths` 内なら破棄してmergeが進む — 設定したファイルをローカルとremoteの両方で変更した状態から、HEADがremoteに追いつくこと
2. 1件でもリスト外があれば従来どおり blocked — **リスト内のファイルも破棄されない**こと（部分破棄は禁止。人間が後で状況を再現できなくなるため）
3. 既存の意図的挙動を壊さない — `test_conflicting_dirty_change_is_left_untouched`（`disposable_paths` 未設定で衝突）が変更なしで通ること
4. 衝突しないdirtyは従来どおり温存される — `test_preserves_non_conflicting_dirty_changes` が通ること
5. 破棄したことがログに残る — 破棄時に専用のstatus行が出て、どのパスを捨てたか読めること
6. moorestech repo に実運用の設定が入る — `.moorestech-external-revisions.json` と `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs` が `disposable_paths` に登録されていること
7. 設定が無い repo は挙動が完全に不変 — `disposable_paths` を持たない3 repo（`.agents` / `business` / `cmux-connector`）の経路に変化がないこと

**やらないこと（スコープ境界）:**

- `stash` を使った退避・復元 — 衝突とは定義上「両側が同じファイルを変えた」状態であり、`stash pop` がほぼ確実にコンフリクトして無人のメインクローンを壊す
- 「ahead」「diverged」で止まる挙動の変更 — 今回の対象は behind のみの状態で ff-only が dirty に阻まれるケースだけ
- ピン自動追随（`ExternalRepositorySyncEditor`）の停止・gitignore化 — 直近30日で19コミットの現役機能であり殺さない
- blocked時の人間への通知 — 別課題

## Global Constraints

- 対象ディレクトリ `~/hermes-agent/data/services/always-on/` は**gitリポジトリではない**。commitは行わず、検証はテスト実行で行う
- `repo-auto-pull` は supervisor の periodic サービス（900秒間隔）として無人で回る。人間への確認・対話を挟む処理を足してはならない
- 破棄対象に渡すパスは**必ずgitが報告した出力との積集合**とする。設定値をそのまま `git checkout --` へ渡してはならない
- 既存のログ書式 `repo-auto-pull name=<name> status=<status> <detail>` を守る（`output()` を使う）
- 既存テストは1つも書き換えない。新規テストのみ追加する

---

### Task 1: 衝突判定と破棄をスクリプトへ実装する

**Files:**
- Modify: `~/hermes-agent/data/services/always-on/scripts/repo-auto-pull.py`（`update()` のmerge直前に判定を挿入・ヘルパー2本を追加）
- Test: `~/hermes-agent/data/services/always-on/tests/test_repo_auto_pull.py`（新規テスト2本を追加）

**Interfaces:**
- Produces: 設定キー `disposable_paths`（`list[str]`・repoルートからの相対パス・省略時は空扱い）と、新status `disposable-discarded`

- [ ] **Step 1: 失敗するテストを2本書く**

`tests/test_repo_auto_pull.py` の `test_conflicting_dirty_change_is_left_untouched` の直後に次を追加する:

```python
    def config_with_disposable(self, disposable: list[str]) -> Path:
        path = self.root / "repos.json"
        path.write_text(json.dumps({
            "allowed_root": str(self.root),
            "repos": [{
                "name": "test",
                "path": str(self.repo),
                "branch": "master",
                "disposable_paths": disposable,
            }],
        }))
        return path

    def test_disposable_conflicting_change_is_discarded(self) -> None:
        (self.repo / "base.txt").write_text("local\n")
        expected = self.push_remote_change("base.txt", "remote\n")
        result = self.invoke(self.config_with_disposable(["base.txt"]))
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertEqual(expected, run("git", "rev-parse", "HEAD", cwd=self.repo).stdout.strip())
        self.assertEqual("remote\n", (self.repo / "base.txt").read_text())
        self.assertIn("disposable-discarded", result.stdout)

    def test_partially_disposable_conflict_is_blocked(self) -> None:
        before = run("git", "rev-parse", "HEAD", cwd=self.repo).stdout.strip()
        (self.repo / "base.txt").write_text("local-base\n")
        (self.repo / "other.txt").write_text("seed\n")
        run("git", "add", "other.txt", cwd=self.repo)
        run("git", "commit", "-m", "seed other", cwd=self.repo)
        run("git", "push", cwd=self.repo)
        run("git", "pull", cwd=self.seed)
        before = run("git", "rev-parse", "HEAD", cwd=self.repo).stdout.strip()
        (self.repo / "other.txt").write_text("local-other\n")
        self.push_remote_change("base.txt", "remote-base\n")
        self.push_remote_change("other.txt", "remote-other\n")
        result = self.invoke(self.config_with_disposable(["base.txt"]))
        self.assertNotEqual(0, result.returncode)
        self.assertEqual(before, run("git", "rev-parse", "HEAD", cwd=self.repo).stdout.strip())
        self.assertEqual("local-base\n", (self.repo / "base.txt").read_text())
        self.assertEqual("local-other\n", (self.repo / "other.txt").read_text())
        self.assertIn("blocked", result.stdout)
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd ~/hermes-agent/data/services/always-on && python3 -m unittest tests.test_repo_auto_pull -v`

Expected: `test_disposable_conflicting_change_is_discarded` が FAIL（`disposable-discarded` が出力に無く、mergeも blocked で止まる）。`test_partially_disposable_conflict_is_blocked` は現行実装でも PASS しうる（全衝突が blocked になるため）。既存6テストは PASS

- [ ] **Step 3: ヘルパー2本を実装する**

`scripts/repo-auto-pull.py` の `pull_lfs()` の直後（`def update(` の直前）に次を挿入する:

```python
def changed_paths(repo: Path, *diff_args: str) -> list[str] | None:
    """git diff --name-only の結果をパス一覧で返す。git失敗時はNone"""
    result = git(repo, "diff", "--name-only", *diff_args)
    if result.returncode:
        return None
    return [line for line in result.stdout.splitlines() if line]


def clear_disposable_conflicts(entry: dict[str, object], name: str, repo: Path, target: str) -> bool | None:
    """取り込みを妨げるdirtyを判定し、全部が破棄可なら破棄する。
    戻り値 True=続行可 / False=blocked / None=git失敗"""
    local = changed_paths(repo, "HEAD")
    incoming = changed_paths(repo, f"HEAD..{target}")
    if local is None or incoming is None:
        return None
    conflicting = sorted(set(local) & set(incoming))
    if not conflicting:
        return True
    disposable = {value for value in entry.get("disposable_paths", []) or [] if isinstance(value, str)}
    blocking = [path for path in conflicting if path not in disposable]
    if blocking:
        output(name, "blocked", f"dirty-conflict={' '.join(blocking)[:300]}")
        return False
    discard = git(repo, "checkout", "--", *conflicting)
    if discard.returncode:
        output(name, "blocked", safe_detail(discard))
        return False
    output(name, "disposable-discarded", f"paths={' '.join(conflicting)[:300]}")
    return True
```

破棄対象 `conflicting` は `git diff --name-only` の出力との積集合なので、設定に何を書いてもリポジトリ外のパスは渡らない。

- [ ] **Step 4: update() へ判定を差し込む**

`update()` 内の次の2行:

```python
    old_head = git(repo, "rev-parse", "--short=12", "HEAD").stdout.strip()
    merge = git(repo, "merge", "--ff-only", "--no-edit", target, timeout=timeout)
```

の**直前**に次を挿入する:

```python
    cleared = clear_disposable_conflicts(entry, name, repo, target)
    if cleared is None:
        output(name, "compare-failed", "diff-failed")
        return False
    if not cleared:
        return False

```

- [ ] **Step 5: テストを実行して全部通ることを確認する**

Run: `cd ~/hermes-agent/data/services/always-on && python3 -m unittest tests.test_repo_auto_pull -v`

Expected: 既存6テスト＋新規2テストの計8テストが PASS。特に `test_conflicting_dirty_change_is_left_untouched`（要件3）と `test_preserves_non_conflicting_dirty_changes`（要件4）が変更なしで通ること

---

### Task 2: moorestech repo に実運用の破棄可パスを設定する

**Files:**
- Modify: `~/hermes-agent/data/services/always-on/scripts/repo-auto-pull.json`（`moorestech` エントリのみ）

**Interfaces:**
- Consumes: Task 1 が実装した `disposable_paths`

- [ ] **Step 1: moorestech エントリへ `disposable_paths` を足す**

`repo-auto-pull.json` の `"name": "moorestech"` エントリに `disposable_paths` キーを**追加**する。既存の `path` / `branch` の値は書き換えない。他の3 repo（`.agents` / `business` / `cmux-connector`）は一切触らない（要件7）:

```json
      "disposable_paths": [
        ".moorestech-external-revisions.json",
        "moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs"
      ]
```

`.moorestech-external-revisions.json` は常駐Unityの `ExternalRepositorySyncEditor` が兄弟クローンのHEADへ追随して5〜30秒毎に書き戻すファイル、`_CompileRequester.cs` は `uloop compile` が書き換えるコンパイルトリガーの痕跡。どちらも人間・エージェントの意図を表さない。

- [ ] **Step 2: 設定が壊れていないことを確認する**

Run: `cd ~/hermes-agent/data/services/always-on && python3 -c "import json;print(len(json.load(open('scripts/repo-auto-pull.json'))['repos']))"`

Expected: `4`

- [ ] **Step 3: 実リポジトリに対してドライに1回走らせる**

Run: `cd ~/hermes-agent/data/services/always-on && python3 scripts/repo-auto-pull.py`

Expected: 4 repo 分の `repo-auto-pull name=... status=...` 行が出る。moorestech が `current` か `updated` か `disposable-discarded`→`updated` のいずれかであり、`blocked` でないこと（dirtyが無ければ `current` でよい）

---

### Task 3: moores-code-reviewで全ブランチレビューを実行する

**Files:**
- 変更なし（レビューのみ）

- [ ] **Step 1: moores-code-reviewスキルを起動する**

Run: `moores-code-review` スキルをブランチ全体に対して実行する

Expected: 指摘の統合結果を得る。機械的修正は適用し、設計判断はAskUserQuestionで裁定を仰ぐ

**このタスクは省略不可**（自動実行・ゴール文言による省略不可）。

なお本planの主変更は `~/hermes-agent/` 配下（gitリポジトリ外）のため、ブランチ差分にはplan文書しか現れない。レビューはplan文書と、下記「レビュー時に読ませる実ファイル」を対象に行う:

- `~/hermes-agent/data/services/always-on/scripts/repo-auto-pull.py`
- `~/hermes-agent/data/services/always-on/tests/test_repo_auto_pull.py`
- `~/hermes-agent/data/services/always-on/scripts/repo-auto-pull.json`

---

## 判断記録（ADR）

- [[2026-08-15-repo-auto-pullは設定の破棄可パスで自動生成dirtyを越える]] — 出所: ユーザー裁定 2026-08-15（AskUserQuestion）。棄却: 通知強化のみ／ピン自動追随の停止
- [[2026-08-15-dirty耐性の是正対象はapplyとrepo-auto-pullとする]] — 出所: ユーザー裁定 2026-08-15

planning中に生じた判断:

- **部分破棄を禁止し、1件でもリスト外なら何も捨てない**: 「リスト内だけ先に捨ててリスト外で止まる」設計も可能だが、人間が後から blocked の状況を再現できなくなる。失敗時は現状を完全に保つ方が無人運用では安全。出所: agent前提（拒否権つき）
- **破棄対象はgit出力との積集合に限定**: 設定値を直接 `git checkout --` へ渡すとリポジトリ外を指せる。`git diff --name-only` の出力と積を取れば、設定が何であれ安全側に閉じる。出所: agent前提（防御的設計）
- **`stash` を使わない**: 衝突とは両側が同じファイルを変えた状態であり、ff-only merge 後の `stash pop` はほぼ確実にコンフリクトして無人のメインクローンを壊す（現状の blocked より悪化）。出所: 事実確認（planning中の検証）
- **unityプレイ録画テストは実行しない**: 変更対象がPythonスクリプトと設定JSONで、Unityランタイム挙動に一切触れないため。出所: agent前提
- **同日の「allowlist棄却」裁定との関係**: [[2026-08-15-applyのdirty判定は全面エージェント判断にする]] はエージェントが判断できる文脈での裁定であり、判断能力のない決定論スクリプトには適用されない。この切り分けはユーザー裁定で確認済み。出所: ユーザー裁定 2026-08-15
