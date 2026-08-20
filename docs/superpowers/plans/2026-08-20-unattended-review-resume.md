# 無人レビューの自壊対策（resume回収・abort申告） Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 無人レビューが `findings.json` を書く前にターンを終えて死んだとき、成果物を捨てずに同一セッションを1回だけ resume して回収し、正当な中止（fail-closed）とは機械的に弁別する。

**Architecture:** poller は `claude --session-id <uuid>` でレビューを起動してセッションIDを `state/pr-<N>/review.session` に固定する。`exit=0` かつ `findings.json` 未生成という死に方を検知したら、`$RUNDIR/abort.json`（スキルが正当な中止時にだけ書く申告ファイル）の有無で分岐し、無ければ `claude -r <uuid>` で最終段から回収、あれば従来どおり失敗ラベルへ落とす。レート制限死の再起動も、まっさら再走から resume へ寄せる。スキル側は `pr-adjudicated-apply` と同型のハードルール節を持ち、終了地点を `findings.json` 生成直後（または `abort.json` 書き出し直後）の2つだけに限定する。

**Tech Stack:** Python 3（`poller.py` / `unittest`）、Claude Code CLI（`--session-id` / `-r`）、Markdown（SKILL.md）

## Requirements

設計対話（grill）で確定した要件。受け入れ基準を各行に含む。

1. **自壊の回収**: `exit=0` かつ `findings.json` 未生成のとき、poller は同一セッションを `claude -r` で **1回だけ** resume する。受け入れ基準: 1回目の検知で `launch_claude` が resume 引数つきで呼ばれ `enter_failed` は呼ばれない／2回目の検知で `enter_failed` が呼ばれる。（出所: ユーザー裁定 2026-08-20）
2. **正当な中止との弁別**: `$RUNDIR/abort.json` が存在し、かつ `review.started` より新しい場合は resume せず即失敗にし、`reason` を失敗コメントへ転記する。受け入れ基準: abort.json ありで `launch_claude` が呼ばれず `enter_failed` が1回呼ばれる／前回runの古い abort.json は無視される。（出所: ユーザー裁定 2026-08-20）
3. **中止の申告義務**: SKILL.md の全中止規定は `$RUNDIR/abort.json`（`reason` / `step` / `at`）を書いてから終える。受け入れ基準: エラー処理節に共通ルールとスキーマが明記されている。（出所: ユーザー裁定 2026-08-20）
4. **レート制限の resume 化**: レート制限死のバックオフ後の再起動を、まっさら再走から同一セッションの resume へ変える。受け入れ基準: レート制限検知時の `launch_claude` が resume 引数つきで呼ばれる。（出所: ユーザー裁定 2026-08-20）
5. **スキル側ハードルール**: `pr-independent-review/SKILL.md` へ「ターンを終えた瞬間にプロセスが死ぬ」節を置き、①待機は同一ターン内でブロッキング ②「後で確認する」で閉じるの禁止 ③終了は `findings.json` か `abort.json` を書いた直後だけ、を明記する。受け入れ基準: 節が存在し、`pr-adjudicated-apply/SKILL.md` の同節と同じ3点を含む。（出所: ユーザー裁定 2026-08-20）
6. **無人判定の明示**: poller は起動時に `PR_REVIEW_UNATTENDED=1` を渡し、SKILL.md はこの環境変数があるときだけ「質問して停止」を禁止する。受け入れ基準: `launch_claude` の env に当該キーが入る／SKILL.md が env 名で条件を書いている。（出所: agent前提。手動発火の対話運用を殺さないため）
7. **resume 上限は1回**: `state/pr-<N>/review.resume` で数え、`handle_waiting` でリセットする。受け入れ基準: 新規起動時に `review.resume` が `0` になる。（出所: agent前提。apply側 `MAX_APPLY_RETRY = 1` と同型）

**やらないこと（スコープ境界）:**

- クラッシュ（`exit != 0` / 終了コード未記録）の再起動は**現状のまっさら再走のまま**変えない。壊れたセッション状態へ resume して同じ死を繰り返す経路を作らないため（resume 経路が実運用で安定したら別タスクで再検討）
- apply フェーズ（`pr-adjudicated-apply` / `handle_applying`）には手を入れない。既に3層対策済み
- 失敗ラベルからの人間の再開操作（ラベル付替え）は現状維持
- `MAX_REVIEW_RETRY = 2`（クラッシュ用リトライ）の値は変えない

## Global Constraints

- **`poller.py` と `test_poller.py` は git 管理外**（`/Users/sakastudio/hermes-agent/data/services/pr-review/`。親ディレクトリを遡ってもリポジトリではない）。このブランチのコミットには含まれない。実サービスの実体を直接編集するため、各タスクは編集後に必ず `python3 -m unittest test_poller.py -v` を全緑にしてから次へ進む
- poller は supervisor の periodic（120秒間隔・`timeout_seconds: 300`）で**毎回プロセスごと起動される**ため、`poller.py` の保存が次tickで自動反映される。supervisor の再起動は不要
- リポジトリ側の変更（SKILL.md・`.decisions/`・本plan）は worktree `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/unattended-review-resume`（branch `fix/unattended-review-resume`）で行う
- Python コードのコメントは既存 `poller.py` の流儀に合わせ、日本語1行 → 英語1行の2行セットで書く
- **デフォルト引数を追加しない**（AGENTS.md 規約）。`claude_argv` / `launch_claude` へ引数を足すときは呼び出し側を全部更新する
- スキルの git 正本は `.agents/skills/` のみ。`.claude/skills` はそこへの symlink なので `.agents/skills/...` のパスを編集する
- 既存テスト `test_clean_exit_without_findings_fails_without_retry` は**旧仕様（即失敗）を固定しているテスト**なので、Task 2 で新仕様のテストへ置き換える（残したまま追加するとテストが矛盾する）

---

### Task 1: セッションIDの固定と無人フラグの受け渡し

**Files:**
- Modify: `/Users/sakastudio/hermes-agent/data/services/pr-review/poller.py:313-314`（`claude_argv`）, `:331-359`（`launch_claude`）, `:477-505`（`handle_waiting`）, および `launch_claude` の全呼び出し箇所
- Test: `/Users/sakastudio/hermes-agent/data/services/pr-review/test_poller.py`

**Interfaces:**
- Produces: `session_id_path(number: int, pid_name: str) -> str`（`state/pr-<N>/review.session` を返す。`pid_name` の `.pid` を `.session` へ置換）
- Produces: `claude_argv(prompt: str, session_id: str, resume: bool) -> list[str]`（`session_id` が空文字なら従来どおりID指定なし／`resume=False` で `--session-id`／`resume=True` で `-r`）
- Produces: `launch_claude(number: int, prompt: str, log_name: str, pid_name: str, cwd: str, session_id: str, resume: bool) -> None`
- Produces: 起動プロセスの env に `PR_REVIEW_UNATTENDED=1`

- [x] **Step 1: 失敗するテストを書く**

`test_poller.py` の `LaunchClaudeTest` へ2本追加する:

```python
    def test_fresh_launch_pins_session_id(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            poller.STATE_DIR = tmpdir
            poller.CLAUDE_BIN = "/fake/claude"
            poller.DRYRUN = False

            with patch.object(poller.subprocess, "Popen") as popen:
                popen.return_value.pid = 12345
                poller.launch_claude(
                    1176, "/pr-independent-review example", "review.log", "review.pid",
                    cwd=tmpdir, session_id="11111111-2222-3333-4444-555555555555", resume=False,
                )

            command = popen.call_args.args[0][2]
            self.assertIn("--session-id 11111111-2222-3333-4444-555555555555", command)
            self.assertNotIn(" -r ", command)
            self.assertEqual(popen.call_args.kwargs["env"]["PR_REVIEW_UNATTENDED"], "1")

    def test_resume_launch_reenters_same_session(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            poller.STATE_DIR = tmpdir
            poller.CLAUDE_BIN = "/fake/claude"
            poller.DRYRUN = False

            with patch.object(poller.subprocess, "Popen") as popen:
                popen.return_value.pid = 12345
                poller.launch_claude(
                    1176, poller.RESUME_PROMPT, "review.log", "review.pid",
                    cwd=tmpdir, session_id="11111111-2222-3333-4444-555555555555", resume=True,
                )

            command = popen.call_args.args[0][2]
            self.assertIn("-r 11111111-2222-3333-4444-555555555555", command)
            self.assertNotIn("--session-id", command)
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `cd /Users/sakastudio/hermes-agent/data/services/pr-review && python3 -m unittest test_poller.LaunchClaudeTest -v`
Expected: FAIL（`launch_claude() got an unexpected keyword argument 'session_id'` / `module 'pr_review_poller' has no attribute 'RESUME_PROMPT'`）

- [x] **Step 3: `poller.py` に実装する**

`import uuid` を `import time` の下（15-22行のimport群、アルファベット順の位置）へ追加する。`MAX_APPLY_RETRY = 1`（102行）の直後へ定数を追加する:

```python
# レビューの自壊（findings.json未生成のターン終了）は同一セッションを1回だけ resume して回収する
# A self-destructed review (turn ended before findings.json) is resumed once to recover its work
MAX_REVIEW_RESUME = 1
# resume時に渡す再開指示。print modeは -r でも新しい入力を要求するため、続きの指示を明示で渡す
# The resume instruction; print mode still needs fresh input with -r, so the continuation is explicit
RESUME_PROMPT = (
    "前回の実行が findings.json を書く前に終了した。同じレビューを中断地点から再開せよ。"
    "$RUNDIR に既にある成果物（agents/*.md・digest.md 等）は捨てず、最終段まで進めて findings.json を生成すること。"
    "質問で停止してはならない（判断が要る指摘はダイジェストの裁定カードへ落とす）。"
    "続行不能な場合のみ $RUNDIR/abort.json に理由を書いてから終えること。"
)
```

`exit_code_path`（317-318行）の直後へ追加する:

```python
def session_id_path(number: int, pid_name: str) -> str:
    return os.path.join(pr_state_dir(number), pid_name.replace(".pid", ".session"))
```

`claude_argv` を差し替える:

```python
def claude_argv(prompt: str, session_id: str, resume: bool) -> list[str]:
    argv = [CLAUDE_BIN, "-p", prompt, "--model", CLAUDE_MODEL, "--dangerously-skip-permissions"]
    # 新規起動はIDを固定し、再開は同じIDへ入り直す。空文字はID管理外の起動（テスト・手動相当）
    # Fresh launches pin the id and resumes re-enter it; an empty id means a launch outside id management
    if session_id:
        argv += ["-r", session_id] if resume else ["--session-id", session_id]
    return argv
```

`launch_claude` のシグネチャへ `session_id: str, resume: bool` を追加し、`claude_argv(prompt)` の2箇所（DRYRUNログと `command` 組み立て）を `claude_argv(prompt, session_id, resume)` にする。あわせて env 設定行（344行）の直後へ追加する:

```python
    # スキル側が「質問して停止」を禁止する条件に使う。無人起動であることの唯一の判定材料
    # The skill keys its "never stop to ask" rule off this; it is the only signal that a launch is unattended
    env["PR_REVIEW_UNATTENDED"] = "1"
```

- [x] **Step 4: 全呼び出し側を更新する**

`grep -n "launch_claude(" poller.py` で出る呼び出しを全部更新する。この時点では resume 経路がまだ無いので、レビュー起動以外はすべて `session_id="", resume=False` を渡す。`handle_waiting`（477-505行）だけは新規セッションIDを発行する:

```python
    write_text(os.path.join(pr_state_dir(number), "review.retry"), "0")
    write_text(os.path.join(pr_state_dir(number), "review.resume"), "0")
    write_text(os.path.join(pr_state_dir(number), "review.started"), str(time.time()))
    session_id = str(uuid.uuid4())
    write_text(session_id_path(number, "review.pid"), session_id)
    launch_claude(
        number,
        prompt=f"/pr-independent-review https://github.com/{REPO}/pull/{number}",
        log_name="review.log",
        pid_name="review.pid",
        cwd=CLONE_DIR,
        session_id=session_id,
        resume=False,
    )
```

- [x] **Step 5: テストを実行して全緑を確認する**

Run: `cd /Users/sakastudio/hermes-agent/data/services/pr-review && python3 -m unittest test_poller -v`
Expected: 全テストPASS（既存13本 + 新規2本）

- [x] **Step 6: コミットする**

`poller.py` は git 管理外なのでコミット対象は無い。plan のチェックボックス更新のみ worktree 側でコミットする:

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/unattended-review-resume
git add docs/superpowers/plans/2026-08-20-unattended-review-resume.md
git commit -m "chore: Task 1完了（pollerのセッションID固定・無人フラグ）"
```

---

### Task 2: 自壊の1回resume と abort.json による弁別

**Files:**
- Modify: `/Users/sakastudio/hermes-agent/data/services/pr-review/poller.py:611-628`（`handle_running` の `exit=0` 分岐）
- Test: `/Users/sakastudio/hermes-agent/data/services/pr-review/test_poller.py:147-190`（`ReviewSelfTerminationTest`。`test_clean_exit_without_findings_fails_without_retry` を置き換え）

**Interfaces:**
- Consumes: Task 1 の `session_id_path` / `launch_claude(..., session_id, resume)` / `MAX_REVIEW_RESUME` / `RESUME_PROMPT`
- Produces: `read_abort(number: int, started_at: float) -> str | None`（`$RUNDIR/abort.json` が存在し `started_at` より新しければ `reason` 文字列、無ければ `None`。JSONパース不能・`reason` 欠落時は `"(reasonなし)"` を返して申告ありとして扱う）

- [x] **Step 1: 失敗するテストを書く**

`test_poller.py:147` の `ReviewSelfTerminationTest` から `test_clean_exit_without_findings_fails_without_retry` を**削除し**、同クラスへ4本入れる（`setUp` は既存のものをそのまま使う。`poller.rundir(4001)` の実体が要るテストでは `os.makedirs` する）:

```python
    def test_clean_exit_without_findings_resumes_once(self):
        sdir = poller.pr_state_dir(4001)
        poller.write_text(os.path.join(sdir, "review.exit"), "0")
        poller.write_text(poller.session_id_path(4001, "review.pid"), "sess-4001")

        with patch.object(poller, "launch_claude") as launch, patch.object(poller, "enter_failed") as failed:
            still_running = poller.handle_running(self.pr)

        self.assertTrue(still_running)
        failed.assert_not_called()
        self.assertEqual(launch.call_args.kwargs["session_id"], "sess-4001")
        self.assertTrue(launch.call_args.kwargs["resume"])
        self.assertEqual(poller.read_int(os.path.join(sdir, "review.resume")), 1)

    def test_second_clean_exit_fails_after_resume_budget(self):
        sdir = poller.pr_state_dir(4001)
        poller.write_text(os.path.join(sdir, "review.exit"), "0")
        poller.write_text(os.path.join(sdir, "review.resume"), "1")

        with patch.object(poller, "launch_claude") as launch, patch.object(poller, "enter_failed") as failed:
            still_running = poller.handle_running(self.pr)

        self.assertFalse(still_running)
        launch.assert_not_called()
        failed.assert_called_once()

    def test_abort_json_fails_without_resume(self):
        os.makedirs(poller.rundir(4001), exist_ok=True)
        poller.write_text(os.path.join(poller.pr_state_dir(4001), "review.exit"), "0")
        poller.write_text(
            os.path.join(poller.rundir(4001), "abort.json"),
            json.dumps({"reason": "SKILL.md同一性ガードで差分検知", "step": "Step 0"}),
        )

        with patch.object(poller, "launch_claude") as launch, patch.object(poller, "enter_failed") as failed:
            still_running = poller.handle_running(self.pr)

        self.assertFalse(still_running)
        launch.assert_not_called()
        self.assertIn("SKILL.md同一性ガード", failed.call_args.kwargs["extra"])

    def test_stale_abort_json_from_previous_run_is_ignored(self):
        os.makedirs(poller.rundir(4001), exist_ok=True)
        abort_path = os.path.join(poller.rundir(4001), "abort.json")
        poller.write_text(abort_path, json.dumps({"reason": "前回runの残骸"}))
        os.utime(abort_path, (0, 0))
        poller.write_text(os.path.join(poller.pr_state_dir(4001), "review.exit"), "0")

        with patch.object(poller, "launch_claude") as launch, patch.object(poller, "enter_failed") as failed:
            poller.handle_running(self.pr)

        launch.assert_called_once()
        failed.assert_not_called()
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `cd /Users/sakastudio/hermes-agent/data/services/pr-review && python3 -m unittest test_poller.ReviewSelfTerminationTest -v`
Expected: FAIL（`module 'pr_review_poller' has no attribute 'read_abort'` / resumeされず `enter_failed` が呼ばれる）

- [x] **Step 3: `read_abort` を実装する**

`read_exit_code`（321-328行）の直後へ追加する:

```python
def read_abort(number: int, started_at: float) -> str | None:
    """スキルが書いた中止申告。今回の起動より新しいものだけを有効とみなす。
    The abort notice written by the skill; only one newer than this launch counts."""
    path = os.path.join(rundir(number), "abort.json")
    if not os.path.exists(path) or os.path.getmtime(path) <= started_at:
        return None
    try:
        with open(path, "r", encoding="utf-8") as f:
            return str(json.load(f).get("reason") or "(reasonなし)")
    # 外部入力JSONのパース隔離。壊れた申告でも「中止した事実」は有効なので握って既定文言へ倒す
    # Boundary isolation for external JSON: a broken notice still proves an abort, so fall back to a default
    except (OSError, json.JSONDecodeError):
        return "(abort.json読み取り不能)"
```

- [x] **Step 4: `handle_running` の `exit=0` 分岐を差し替える**

611-628行の「正常終了なのにfindingsが無い」ブロックを次で置き換える:

```python
    # 正常終了なのにfindingsが無い。abort.jsonがあれば正当な中止（fail-closed）、無ければターン終了による自壊。
    # 自壊は成果物が$RUNDIRに残っているので、同一セッションを1回だけresumeして最終段を回収する（ユーザー裁定 2026-08-20）
    # A clean exit without findings is a legitimate abort when abort.json exists, otherwise a turn-end self-destruct;
    # the latter keeps its artifacts in $RUNDIR, so resume the same session once to recover the final stage
    if read_exit_code(number, "review.pid") == 0:
        aborted = read_abort(number, started_at)
        if aborted is not None:
            if DRYRUN:
                log(f"[DRYRUN] pr-{number}: レビューが中止を申告、失敗へ遷移予定")
                return False
            enter_failed(
                pr,
                marker_name="failed_comment_review",
                log_name="review.log",
                extra=f"（レビューが中止を申告: {aborted}）",
            )
            return False
        resume_path = os.path.join(sdir, "review.resume")
        resume_count = read_int(resume_path, default=0)
        if resume_count < MAX_REVIEW_RESUME:
            if DRYRUN:
                log(f"[DRYRUN] pr-{number}: レビュー自力終了、resume予定 (attempt {resume_count + 1})")
                return True
            write_text(resume_path, str(resume_count + 1))
            write_text(started_path, str(time.time()))
            log(f"pr-{number}: レビュー自力終了（exit=0・findings無し）、同一セッションをresume ({resume_count + 1}/{MAX_REVIEW_RESUME})")
            launch_claude(
                number,
                prompt=RESUME_PROMPT,
                log_name="review.log",
                pid_name="review.pid",
                cwd=CLONE_DIR,
                session_id=read_text(session_id_path(number, "review.pid")) or "",
                resume=True,
            )
            return True
        if DRYRUN:
            log(f"[DRYRUN] pr-{number}: resume上限到達、失敗へ遷移予定")
            return False
        enter_failed(
            pr,
            marker_name="failed_comment_review",
            log_name="review.log",
            extra="（レビューが自力終了・findings未生成: resume 1回でも回収できず）",
        )
        return False
```

- [x] **Step 5: テストを実行して全緑を確認する**

Run: `cd /Users/sakastudio/hermes-agent/data/services/pr-review && python3 -m unittest test_poller -v`
Expected: 全テストPASS

- [x] **Step 6: コミットする**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/unattended-review-resume
git add docs/superpowers/plans/2026-08-20-unattended-review-resume.md
git commit -m "chore: Task 2完了（自壊のresume回収とabort.json弁別）"
```

---

### Task 3: レート制限再起動の resume 化

**Files:**
- Modify: `/Users/sakastudio/hermes-agent/data/services/pr-review/poller.py:591-609`（`handle_running` のレート制限分岐）
- Test: `/Users/sakastudio/hermes-agent/data/services/pr-review/test_poller.py`

**Interfaces:**
- Consumes: Task 1 の `session_id_path` / `launch_claude(..., session_id, resume)` / `RESUME_PROMPT`

- [x] **Step 1: 失敗するテストを書く**

`ReviewSelfTerminationTest` へ追加する:

```python
    def test_rate_limit_relaunch_resumes_same_session(self):
        sdir = poller.pr_state_dir(4001)
        poller.write_text(os.path.join(sdir, "review.log"), "You've hit your session limit · resets 11:50pm")
        poller.write_text(poller.session_id_path(4001, "review.pid"), "sess-4001")

        with patch.object(poller, "launch_claude") as launch, patch.object(poller, "enter_failed") as failed:
            still_running = poller.handle_running(self.pr)

        self.assertTrue(still_running)
        failed.assert_not_called()
        self.assertEqual(launch.call_args.kwargs["session_id"], "sess-4001")
        self.assertTrue(launch.call_args.kwargs["resume"])
        self.assertEqual(poller.read_int(os.path.join(sdir, "review.resume"), default=0), 0)
```

`review.resume` が増えないことも確認する（レート制限は自壊ではないため resume 予算を消費しない）。

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `cd /Users/sakastudio/hermes-agent/data/services/pr-review && python3 -m unittest test_poller -v -k rate_limit`
Expected: FAIL（`session_id` が `""`、`resume` が `False`）

- [x] **Step 3: レート制限分岐を差し替える**

601-609行の `launch_claude(...)` 呼び出しを置き換える（`set_backoff` / `write_text(started_path, ...)` の2行はそのまま残す）:

```python
        # レート制限は自壊ではないので resume 予算を消費しない。中断地点から同じセッションで続ける
        # A rate limit is not a self-destruct, so it spends no resume budget; continue the same session
        log(f"pr-{number}: レート制限検知、リトライ回数を消費せず同一セッションをresume (rate limit; resuming without consuming a retry)")
        launch_claude(
            number,
            prompt=RESUME_PROMPT,
            log_name="review.log",
            pid_name="review.pid",
            cwd=CLONE_DIR,
            session_id=read_text(session_id_path(number, "review.pid")) or "",
            resume=True,
        )
        return True
```

- [x] **Step 4: テストを実行して全緑を確認する**

Run: `cd /Users/sakastudio/hermes-agent/data/services/pr-review && python3 -m unittest test_poller -v`
Expected: 全テストPASS

- [x] **Step 5: コミットする**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/unattended-review-resume
git add docs/superpowers/plans/2026-08-20-unattended-review-resume.md
git commit -m "chore: Task 3完了（レート制限再起動のresume化）"
```

---

### Task 4: SKILL.md のハードルール節と abort.json 申告義務

**Files:**
- Modify: `.agents/skills/pr-independent-review/SKILL.md`（16行目の見出し直後へ新節を挿入 / 「## エラー処理」節の冒頭へ共通ルールを追加）
- Modify: `/Users/sakastudio/hermes-agent/data/services/pr-review/README.md`（「## 失敗時の人間の再開操作」と「レビュー失敗のみ最大2回リトライする」の記述を新仕様へ更新）

**Interfaces:**
- Consumes: Task 2 の `read_abort` が読む `$RUNDIR/abort.json` のスキーマ（`reason` / `step` / `at`）
- Consumes: Task 1 が渡す `PR_REVIEW_UNATTENDED=1`

- [x] **Step 1: ハードルール節を挿入する**

`# pr-independent-review — 独立セッションPRレビュー（シャドー運用v1）`（16行目）と `対応spec:` の行の間へ挿入する:

```markdown
## 最重要: ターンを終えた瞬間にプロセスが死ぬ

環境変数 `PR_REVIEW_UNATTENDED=1` が立っているとき、このスキルは poller から `claude -p`（print mode）で
detach起動されている。**print modeにはターンの続きが無い** — アシスタントのターンが終わった時点でプロセスがexitし、
以後あなたは二度と再開されない。`findings.json` を書かずに終われば、それは poller から自壊と見なされる
（実際にPR1176・PR1178で発生。ユーザー裁定 2026-08-20）。

したがって無人起動時は:

- **待機は必ず同一ターン内でブロッキングして行う**。subagentの完了待ちは、そのターンで待ち切る。
  完了に数十分かかっても、待つこと自体がこのスキルの仕事である
- **「残り1本の完了を待ちます」「後で結果を確認します」と述べてターンを閉じることを禁止する**。
  スケジュールされた再開はこの実行環境に存在しない
- **質問して停止することを禁止する**。判断が要る指摘はダイジェストの裁定カード（設計判断）へ落とす。
  裁定サイトが人間へ渡す経路であり、ターンを閉じて聞くのは経路ではない
- **終了地点は2つだけ** — Step 7.5の `findings.json` が生成された直後か、下記「中止の申告」で
  `abort.json` を書いた直後。どちらも書かずに終わる終わり方は、成功・失敗いずれの意図であってもバグである

`PR_REVIEW_UNATTENDED` が無い（人が対話で起動した）場合は、質問して止まってよい。ただし
`findings.json` / `abort.json` のどちらかで終える規律は同じく守る。

### 中止の申告（abort.json）

「エラー処理」節のどの規定で中止するときも、**終わる前に `$RUNDIR/abort.json` を書く**。
これが無いままの終了は poller から自壊と見なされ、同一セッションが1回 resume される（＝人間を呼ぶべき
fail-closedが、押し切られて続行される）。`$RUNDIR` がまだ無い段階での中止なら `mkdir -p` してから書く。

```json
{"reason": "<中止理由の一行>", "step": "<中止したStep名>", "at": "<ISO8601>"}
```

`reason` は失敗コメントへそのまま転記されるので、人間が次の一手を決められる粒度で書く。
```

- [x] **Step 2: エラー処理節の冒頭へ共通ルールを追加する**

`## エラー処理`（挿入により行番号がずれるので `grep -n "^## エラー処理" SKILL.md` で位置を取る）の直後、
最初の箇条書きの前へ1行入れる:

```markdown
**下記のどの中止でも、終わる前に `$RUNDIR/abort.json` を書く**（冒頭「中止の申告」節。書かずに終わると自壊と誤認されresumeされる）。
```

- [x] **Step 3: 挿入結果を確認する**

Run: `grep -n "PR_REVIEW_UNATTENDED\|abort.json" .agents/skills/pr-independent-review/SKILL.md`
Expected: ハードルール節・中止の申告節・エラー処理節冒頭の3箇所すべてにヒットする

- [x] **Step 4: README.md を更新する**

`/Users/sakastudio/hermes-agent/data/services/pr-review/README.md` の以下2点を書き換える:

1. 「## 失敗時の人間の再開操作」の説明へ、`exit=0`＋findings無しは**自動で1回resumeされる**こと、
   `abort.json` 申告つきの中止だけが即失敗になることを1段落追記する
2. 「apply失敗はリトライしない仕様（spec通り）。レビュー失敗のみ最大2回リトライする。」の行へ、
   「加えてレビューの自壊（exit=0・findings未生成）は同一セッションを1回だけresumeして回収する
   （`review.resume`。abort.json申告がある中止は対象外）」を追記する

- [x] **Step 5: コミットする**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/unattended-review-resume
git add .agents/skills/pr-independent-review/SKILL.md docs/superpowers/plans/2026-08-20-unattended-review-resume.md
git commit -m "docs(pr-independent-review): 無人実行のターン終了自壊を禁じるハードルールとabort.json申告を追加"
```

---

### Task 5: 実サービスでの反映確認

**Files:**
- Read only: `/Users/sakastudio/hermes-agent/data/services/pr-review/state/poller.log`, `state/pr-<N>/review.session`

**Interfaces:**
- Consumes: Task 1〜3 の `poller.py`

- [x] **Step 1: 構文と全テストの最終確認**

Run: `cd /Users/sakastudio/hermes-agent/data/services/pr-review && python3 -m py_compile poller.py && python3 -m unittest test_poller -v`
Expected: 例外なし・全テストPASS

- [x] **Step 2: DRYRUNで1tick流して既存PRの状態遷移が壊れていないことを見る**

Run: `cd /Users/sakastudio/hermes-agent/data/services/pr-review && PR_REVIEW_DRYRUN=1 python3 poller.py`
Expected: 例外で落ちず、管理対象PRごとに `[DRYRUN]` 行か状態行が出る

- [ ] **Step 3: 次tick以降の実起動でセッションIDが記録されることを確認する**

次にレビューが起動したPRで確認する（すぐに起動が無ければこのステップは「未確認」と記録して先へ進んでよい）:

Run: `ls ~/hermes-agent/data/services/pr-review/state/pr-*/review.session && grep "launched claude" ~/hermes-agent/data/services/pr-review/state/poller.log | tail -3`
Expected: `review.session` にUUIDが入っており、起動ログが出ている

- [x] **Step 4: bd issue をクローズする**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/unattended-review-resume
bd close moorestech-616u --reason="poller resume/abort弁別とSKILL.mdハードルールを実装"
```

- [x] **Step 5: コミットしてpush・PR作成する**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/unattended-review-resume
git add -A
git commit -m "chore: Task 5完了（実サービス反映確認）"
git push -u origin fix/unattended-review-resume
```

---

### Task 6: 全ブランチレビュー（必須・省略不可）

**Files:**
- Read only: このブランチの全差分

- [ ] **Step 1: moores-code-review スキルで全ブランチレビューを実行する**

`moores-code-review` スキルを起動し、`master...fix/unattended-review-resume` の全差分をレビューする。
**このタスクは自動実行であり、ゴール達成を理由に省略できない。**

- [ ] **Step 2: 指摘へ対応し、ユーザー裁定が要るものは裁定に出す**

- [ ] **Step 3: 対応をコミットする**

---

## 判断記録（ADR）

設計裁定の正本: `.decisions/2026-08-20-無人レビューの自壊対策はresumeとabort申告で入れる.md`
（前例: `.decisions/2026-08-17-無人applyの死亡対策は3層で入れる.md` / `.decisions/2026-08-17-失敗した独立レビューは最終段だけ再開する.md`）

| 判断 | 内容 | 出所 |
| --- | --- | --- |
| 自壊は即失敗でなくresumeで回収する | `exit=0`＋findings無しを1回だけ `claude -r` で再開 | ユーザー裁定 2026-08-20 |
| 正当な中止は abort.json で申告させる | 申告あり＝即失敗、なし＝自壊とみなしresume | ユーザー裁定 2026-08-20 |
| レート制限死もresumeにする | まっさら再走をやめ、中断地点から続ける | ユーザー裁定 2026-08-20 |
| resume上限は1回 | apply側 `MAX_APPLY_RETRY = 1` と同型。使い切れば従来どおり失敗ラベル | agent前提（apply側の前例） |
| 無人判定は `PR_REVIEW_UNATTENDED=1` | env一本で判定し、手動発火の対話（質問して止まる運用）は殺さない | agent前提（手動発火がskill descriptionに明記された正規経路のため） |
| クラッシュ（exit≠0）は現状のまっさら再走のまま | 壊れたセッションへresumeして同じ死を繰り返す経路を作らない | agent前提（保守的な縮退） |
| ハードルール節は apply 側の文言を移植する | 同じ死に方には同じ言い回しで対処し、2スキル間の非対称をなくす | agent前提（前例踏襲） |
| レート制限resumeは resume予算を消費しない | レート制限は自壊ではなく外的要因。予算を焼くと本来の回収枠が消える | agent前提 |

**配置と前例:**

- ハードルール節の配置 → `pr-adjudicated-apply/SKILL.md:15-33`（タイトル直後）と同位置・同構成
- resume回数・セッションIDの状態ファイル → `state/pr-<N>/` 配下（`review.pid` / `review.exit` / `review.retry` と同じ場所・同じ命名規則）
- 起動の唯一の入口は `launch_claude` のまま（第2の起動経路を作らない）
- `abort.json` の置き場は `$RUNDIR`（`findings.json` と同居。run単位の成果物という同じ性格で、`rundir()` が最新runを解決する既存機構にそのまま乗る）

**機能パリティ（死活表）:**

| 現在できる操作 | 変更後 | 根拠 |
| --- | --- | --- |
| 失敗ラベル→待ちラベルの手動再開 | 生存 | ラベル遷移のロジックは無改修 |
| 手動 `/pr-independent-review` で質問しながら進める | 生存 | 質問禁止は `PR_REVIEW_UNATTENDED` がある時だけ |
| クラッシュ時の最大2回リトライ | 生存 | `MAX_REVIEW_RETRY` と当該分岐は無改修 |
| レート制限時の自動再開 | 変化（まっさら再走→resume） | ユーザー裁定 2026-08-20 |
| `exit=0`＋findings無しで即失敗し人間を呼ぶ | 変化（1回resume後に失敗。abort.json申告時は即失敗のまま） | ユーザー裁定 2026-08-20 |
