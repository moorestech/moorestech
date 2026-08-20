# pr-review poller: cmuxフォアグラウンド起動・同時2本・reset後継続 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** pr-review poller のレビュー/apply 起動を `claude -p`（headless detach）から cmux ワークスペース上の対話モード claude へ切り替え、レビュー同時起動を2本に制限し、session limit 死を「reset 後に同じペインへ継続指示を送る」一時停止へ格下げする。

**Architecture:** poller（`~/hermes-agent/data/services/pr-review/poller.py`・periodic 120s）に2つの小モジュールを足す。`cmux_launcher.py` が cmux CLI（`workspace create`/`list-workspaces`/`send`/`send-key`/`capture-pane`/`workspace close`/`ping`）を包み、`transcript_probe.py` が固定 session-id の transcript jsonl（`~/.claude/projects/*/<session-id>.jsonl` と `<session-id>/subagents/*.jsonl`）から「最終 assistant 発話・limit 種別と reset 時刻・最終活動時刻」を読む。poller の検知は review.log（stdout）から transcript＋findings/result ファイル＋プロセス生存（`pgrep -f "session-id <id>"`）へ移る。ラベルが再開チェックポイントの正である設計は維持。

**Tech Stack:** Python 3（標準ライブラリのみ）、unittest（`python3 -m unittest test_poller -v`）、cmux CLI `/Applications/cmux.app/Contents/Resources/bin/cmux`、Claude Code CLI 2.1.237。

## Requirements

（出所: `.decisions/2026-08-20-無人レビューは同時2本までcmuxフォアグラウンドで起動し限界死はreset後にSendMessageで継続する.md`、`docs/adr/0023-unattended-review-runs-in-cmux-foreground.md`）

- R1 レビュー同時起動は2本まで。`独立レビュー待ち` が3本以上あっても、実行中（バックオフ中を含む）が2本なら見送り、次 tick で再評価する。受け入れ: 待ち3本・実行中0本の tick で起動は2本、ログに「レビュー同時起動上限のため見送り」が1本分出る。
- R2 レビュー・apply の起動は cmux ワークスペース（名前 `pr-review <N>` / `pr-apply <N>`、cwd は従来どおり CLONE_DIR / apply スロット）で**対話モード**の claude を `--session-id <uuid> --model claude-opus-5 --dangerously-skip-permissions "<プロンプト>"` で起動する。`-p` は使わない。受け入れ: 起動後に `list-workspaces` に当該名があり、`pgrep -f "session-id <uuid>"` が生きている。
- R3 プロンプト冒頭に無人運用宣言（`UNATTENDED_PREFACE`）を付け、環境変数 `PR_REVIEW_UNATTENDED=1`・`HOME=<REAL_HOME>` をコマンド文字列の `env` で渡す。受け入れ: 生成コマンド文字列に両方が含まれる（テストで検証）。
- R4 `cmux ping` が失敗する tick は起動を見送り、1時間に1回だけ Discord へ「cmux 不在のため無人パイプライン停止中」を通知する。`-p` フォールバックは持たない。
- R5 完了検知は従来どおり `findings.json`（head 一致・started 以降）/ `apply-result.json`。生存検知は `pgrep -f "session-id <id>"`。review.log / apply.log は書かない（起動マーカー行も不要）。
- R6 session limit（transcript の最終 assistant 発話、または capture-pane のスクリーンに `hit your session limit · resets <時刻>`）を検知したら、reset 時刻＋300秒を `ratelimit.until` に書き、到来後に**同じワークスペースへ** `LIMIT_CONTINUE_PROMPT` を `send`＋`send-key Enter` で投入する。時刻が読めない時は 1800 秒後。リトライ予算は消費しない。受け入れ: fixture transcript で `resets 11:50pm` → until が当日/翌日の 23:55 JST になる。
- R7 weekly limit（`hit your weekly limit`）を検知したら失敗ラベルへ遷移して人を呼ぶ（コメントに「週次上限・アカウント切替が必要」）。
- R8 プロセスが生きているのに findings/result が無く、session と subagents の transcript 最終更新から `IDLE_SECONDS`（1200秒）以上経過した場合は「自壊相当」とみなし、同じワークスペースへ `RESUME_PROMPT` を投入する（予算 `MAX_REVIEW_RESUME=1`、超過で失敗）。
- R9 プロセスが死んでいて findings/result が無く limit でもない場合は従来どおり retry（review 2回・apply 1回）で**新セッション・新ワークスペース**を起動し、古いワークスペースは閉じる。
- R10 findings.json 検出（レビュー完了）・apply success で当該ワークスペースを `workspace close`。失敗ラベルへ遷移したワークスペースは残す。
- R11 失敗コメントの末尾抜粋は review.log の tail ではなく、transcript の最終 assistant 発話（無ければ capture-pane の末尾20行）を使う。
- R12 apply にも session-id を発行し（`apply.session`）、同じ検知経路を使う。
- R13 スキル側ドキュメント（`pr-independent-review/SKILL.md` 冒頭「ターンを終えた瞬間にプロセスが死ぬ」節、`pr-adjudicated-apply/SKILL.md` の同趣旨箇所があればそれ）と `services/pr-review/README.md` を新起動方式に合わせて更新する。「findings.json / abort.json のどちらかで終える」規律はそのまま。
- やらないこと: 直近5h消費を見た起動ゲート／時間帯ゲート、moores-code-review オーケストレータの待機規律・poll-guard 拡張・SKILL.md の監督コスト訂正（並行セッション担当）、cmux 不在時の `-p` フォールバック、レビュー体数・モデルの変更。

## Global Constraints

- poller 本体は `~/hermes-agent/data/services/pr-review/`（git 管理外・supervisor の periodic サービス `pr-review-poller`、120秒、timeout 300秒）。編集はその場で行い、`python3 -m unittest test_poller -v` が全緑であること。supervisor は `services.json` を5秒ごとに再読込するため再起動不要。
- 新規モジュールは各200行以下。`try-catch` は外部境界（subprocess 起動・JSON パース・ファイル I/O）に限り、境界である根拠をコメントで書く。コメントは日本語→英語の2行セット。
- 既存の関数名・状態ファイル名（`review.pid`/`review.session`/`review.retry`/`review.resume`/`review.started`/`ratelimit.until`/`apply.*`/marker）は残す。新規ファイルは `review.workspace` / `apply.workspace`（cmux ワークスペース UUID）。
- cmux CLI は `CMUX_BIN = /Applications/cmux.app/Contents/Resources/bin/cmux`、呼び出し時は `CMUX_QUIET=1` を env に入れる（alias 警告を抑止）。ワークスペースは必ず **UUID** で扱う（`workspace:NN` の ref は close で詰まって変わる）。
- DRYRUN（`PR_REVIEW_DRYRUN=1`）では cmux も claude も呼ばず、従来どおり `[DRYRUN]` ログのみ。
- 実機の前提（本セッションで確認済み）: `cmux ping` は clean env から PONG、`workspace create --name --cwd --command --focus false` は `OK workspace:NN` を返し UUID は `list-workspaces --id-format both` で名前から引く、`send <text>` は改行を送らず `send-key Enter` が要る、`capture-pane --workspace <uuid> --lines N` でスクリーン末尾が取れる、`workspace close --workspace <uuid>` で閉じる。
- 実機で未確認の点（実装時に Task 9 の smoke で確認する）: 対話モードで session limit に達したとき、その文言が transcript に assistant 発話として記録されるか。記録されない場合に備えて capture-pane も併読する（R6）。

---

### Task 1: cmux_launcher.py — cmux CLI ラッパー

**Files:**
- Create: `~/hermes-agent/data/services/pr-review/cmux_launcher.py`
- Test: `~/hermes-agent/data/services/pr-review/test_cmux_launcher.py`

**Interfaces:**
- Produces:
  - `CMUX_BIN: str`
  - `ping() -> bool`
  - `create_workspace(name: str, cwd: str, command: str) -> str | None` — 作成後に名前から UUID を引いて返す。失敗時 None
  - `find_workspace_uuid(name: str) -> str | None`
  - `send_text(workspace_uuid: str, text: str) -> bool` — `send` ＋ `send-key Enter`
  - `capture_tail(workspace_uuid: str, lines: int) -> str`
  - `close_workspace(workspace_uuid: str) -> bool`
  - `workspace_exists(workspace_uuid: str) -> bool`

- [ ] **Step 1: 失敗するテストを書く**

```python
# test_cmux_launcher.py
import importlib.util
import unittest
from pathlib import Path
from unittest.mock import patch

MOD_PATH = Path(__file__).with_name("cmux_launcher.py")
spec = importlib.util.spec_from_file_location("cmux_launcher", MOD_PATH)
cmux = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(cmux)


class FakeCompleted:
    def __init__(self, stdout="", returncode=0):
        self.stdout = stdout
        self.returncode = returncode


class CmuxLauncherTest(unittest.TestCase):
    def test_ping_true_on_pong(self):
        with patch.object(cmux.subprocess, "run", return_value=FakeCompleted("PONG\n", 0)) as run:
            self.assertTrue(cmux.ping())
        self.assertEqual(run.call_args.args[0][:2], [cmux.CMUX_BIN, "ping"])
        self.assertEqual(run.call_args.kwargs["env"]["CMUX_QUIET"], "1")

    def test_ping_false_on_error(self):
        with patch.object(cmux.subprocess, "run", side_effect=OSError("no socket")):
            self.assertFalse(cmux.ping())

    def test_create_workspace_returns_uuid_resolved_by_name(self):
        listing = "  workspace:28 02A4A452-C3FD-4781-8AE6-62DF315A1AB9  pr-review 1189\n"
        outputs = [FakeCompleted("OK workspace:28\n"), FakeCompleted(listing)]
        with patch.object(cmux.subprocess, "run", side_effect=outputs) as run:
            uuid = cmux.create_workspace("pr-review 1189", "/tmp/clone", "echo hi")
        self.assertEqual(uuid, "02A4A452-C3FD-4781-8AE6-62DF315A1AB9")
        create_argv = run.call_args_list[0].args[0]
        self.assertEqual(create_argv[1:3], ["workspace", "create"])
        self.assertIn("--cwd", create_argv)
        self.assertIn("/tmp/clone", create_argv)
        self.assertIn("--command", create_argv)
        self.assertIn("echo hi", create_argv)
        self.assertIn("--focus", create_argv)
        self.assertIn("false", create_argv)

    def test_create_workspace_none_when_name_not_listed(self):
        outputs = [FakeCompleted("OK workspace:28\n"), FakeCompleted("  workspace:1 AAAAAAAA-0000-0000-0000-000000000000  other\n")]
        with patch.object(cmux.subprocess, "run", side_effect=outputs):
            self.assertIsNone(cmux.create_workspace("pr-review 1189", "/tmp", "true"))

    def test_send_text_sends_text_then_enter(self):
        with patch.object(cmux.subprocess, "run", return_value=FakeCompleted("OK surface:1 workspace:2\n")) as run:
            self.assertTrue(cmux.send_text("02A4A452-C3FD-4781-8AE6-62DF315A1AB9", "続きをやれ"))
        first, second = run.call_args_list[0].args[0], run.call_args_list[1].args[0]
        self.assertEqual(first[1], "send")
        self.assertEqual(first[-1], "続きをやれ")
        self.assertEqual(second[1:2], ["send-key"])
        self.assertEqual(second[-1], "Enter")

    def test_capture_tail_returns_stdout(self):
        with patch.object(cmux.subprocess, "run", return_value=FakeCompleted("line1\nline2\n")):
            self.assertEqual(cmux.capture_tail("02A4A452-C3FD-4781-8AE6-62DF315A1AB9", 20), "line1\nline2")

    def test_close_workspace_uses_uuid(self):
        with patch.object(cmux.subprocess, "run", return_value=FakeCompleted("OK workspace:28\n")) as run:
            self.assertTrue(cmux.close_workspace("02A4A452-C3FD-4781-8AE6-62DF315A1AB9"))
        argv = run.call_args.args[0]
        self.assertEqual(argv[1:3], ["workspace", "close"])
        self.assertIn("02A4A452-C3FD-4781-8AE6-62DF315A1AB9", argv)

    def test_workspace_exists_checks_listing(self):
        listing = "  workspace:28 02A4A452-C3FD-4781-8AE6-62DF315A1AB9  pr-review 1189\n"
        with patch.object(cmux.subprocess, "run", return_value=FakeCompleted(listing)):
            self.assertTrue(cmux.workspace_exists("02A4A452-C3FD-4781-8AE6-62DF315A1AB9"))
            self.assertFalse(cmux.workspace_exists("FFFFFFFF-0000-0000-0000-000000000000"))


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd ~/hermes-agent/data/services/pr-review && python3 -m unittest test_cmux_launcher -v`
Expected: FAIL（`cmux_launcher.py` が無く import エラー）

- [ ] **Step 3: 実装を書く**

```python
# cmux_launcher.py
"""cmux CLI ラッパー。poller が対話モード claude をフォアグラウンド起動し、後から入力を送るための最小API。
Thin wrapper over the cmux CLI so the poller can launch interactive claude in a workspace and drive it later.

ワークスペースは UUID で扱う（`workspace:NN` の ref は close で詰まって変わる）。
Workspaces are addressed by UUID; `workspace:NN` refs shift when others close.
"""
import os
import re
import subprocess

CMUX_BIN = os.environ.get("PR_REVIEW_CMUX_BIN", "/Applications/cmux.app/Contents/Resources/bin/cmux")
CMUX_TIMEOUT_SECONDS = 20
UUID_RE = re.compile(r"[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}")


def _run(args: list[str]) -> subprocess.CompletedProcess | None:
    env = os.environ.copy()
    env["CMUX_QUIET"] = "1"
    # 外部境界: cmux ソケットが無い・タイムアウト等はここで握り、呼び出し側は None/False を見る
    # Boundary isolation: a missing socket or timeout is absorbed here; callers only see None/False
    try:
        return subprocess.run(
            [CMUX_BIN] + args, capture_output=True, text=True, timeout=CMUX_TIMEOUT_SECONDS, env=env, check=False
        )
    except (OSError, subprocess.TimeoutExpired):
        return None


def ping() -> bool:
    res = _run(["ping"])
    return bool(res and res.returncode == 0 and "PONG" in res.stdout)


def find_workspace_uuid(name: str) -> str | None:
    res = _run(["list-workspaces", "--id-format", "both"])
    if not res or res.returncode != 0:
        return None
    for line in res.stdout.splitlines():
        # 行末の名前が完全一致するものだけ採る（"pr-review 118" が "pr-review 1189" に当たらないように）
        # Accept only an exact trailing name match so "pr-review 118" never matches "pr-review 1189"
        m = UUID_RE.search(line)
        if m and line.rstrip().endswith(" " + name):
            return m.group(0).upper()
    return None


def create_workspace(name: str, cwd: str, command: str) -> str | None:
    res = _run(["workspace", "create", "--name", name, "--cwd", cwd, "--command", command, "--focus", "false"])
    if not res or res.returncode != 0:
        return None
    return find_workspace_uuid(name)


def send_text(workspace_uuid: str, text: str) -> bool:
    # `send` は改行を送らない。Enter は別コマンドで押す
    # `send` does not append a newline; Enter is a separate key press
    sent = _run(["send", "--workspace", workspace_uuid, text])
    if not sent or sent.returncode != 0:
        return False
    entered = _run(["send-key", "--workspace", workspace_uuid, "Enter"])
    return bool(entered and entered.returncode == 0)


def capture_tail(workspace_uuid: str, lines: int) -> str:
    res = _run(["capture-pane", "--workspace", workspace_uuid, "--lines", str(lines)])
    if not res or res.returncode != 0:
        return ""
    return res.stdout.strip()


def close_workspace(workspace_uuid: str) -> bool:
    res = _run(["workspace", "close", "--workspace", workspace_uuid])
    return bool(res and res.returncode == 0)


def workspace_exists(workspace_uuid: str) -> bool:
    res = _run(["list-workspaces", "--id-format", "both"])
    return bool(res and res.returncode == 0 and workspace_uuid.upper() in res.stdout.upper())
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `python3 -m unittest test_cmux_launcher -v`
Expected: 8 tests PASS

- [ ] **Step 5: 実機スモーク（cmux が起動している状態で）**

Run:
```bash
cd ~/hermes-agent/data/services/pr-review && python3 - <<'EOF'
import cmux_launcher as c
assert c.ping()
u = c.create_workspace("poller-smoke", "/tmp", "sleep 120"); print("uuid", u)
assert u and c.workspace_exists(u)
assert c.send_text(u, "echo smoke-ok")
print(c.capture_tail(u, 5))
assert c.close_workspace(u) and not c.workspace_exists(u)
print("smoke ok")
EOF
```
Expected: `uuid <UUID>` と末尾に `smoke ok`（ワークスペースは閉じられている）

- [ ] **Step 6: 状態確認（コミット対象外）**

poller ディレクトリは git 管理外。`ls -la ~/hermes-agent/data/services/pr-review/` に `cmux_launcher.py` `test_cmux_launcher.py` があることを確認してタスク完了とする。

---

### Task 2: transcript_probe.py — transcript から状態を読む

**Files:**
- Create: `~/hermes-agent/data/services/pr-review/transcript_probe.py`
- Test: `~/hermes-agent/data/services/pr-review/test_transcript_probe.py`

**Interfaces:**
- Produces:
  - `PROJECTS_DIR: str`（既定 `<REAL_HOME>/.claude/projects`、env `PR_REVIEW_CLAUDE_PROJECTS_DIR` で上書き）
  - `find_session_file(session_id: str) -> str | None` — `glob(PROJECTS_DIR/*/<session_id>.jsonl)`
  - `last_assistant(session_id: str) -> tuple[str, float]` — 最終 assistant text ブロックとそのレコードの `timestamp`（epoch。無ければ ("", 0.0)）
  - `last_assistant_text(session_id: str) -> str` — `last_assistant(...)[0]`
  - `last_activity_epoch(session_id: str) -> float` — session jsonl と `<dir>/<session_id>/subagents/*.jsonl` の mtime 最大（ファイル無しは 0.0）
  - `detect_limit(text: str, now_epoch: float) -> tuple[str, float | None] | None` — `("weekly", None)` / `("session", reset_epoch or None)` / None
  - `parse_reset_epoch(text: str, now_epoch: float) -> float | None` — `resets 11:50pm` / `resets 4:50am` を JST で当日→過去なら翌日に解決。`resets Aug 21 at 9pm` 形式は None（weekly は到達時刻を使わない）

- [ ] **Step 1: 失敗するテストを書く**

```python
# test_transcript_probe.py
import datetime
import importlib.util
import json
import os
import tempfile
import unittest
from pathlib import Path

MOD_PATH = Path(__file__).with_name("transcript_probe.py")
spec = importlib.util.spec_from_file_location("transcript_probe", MOD_PATH)
probe = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(probe)

JST = datetime.timezone(datetime.timedelta(hours=9))


def write_session(root: str, session_id: str, records: list[dict], subagent_records: list[dict] | None = None) -> str:
    proj = os.path.join(root, "-Users-sakastudio-clone")
    os.makedirs(proj, exist_ok=True)
    path = os.path.join(proj, f"{session_id}.jsonl")
    with open(path, "w", encoding="utf-8") as f:
        for r in records:
            f.write(json.dumps(r, ensure_ascii=False) + "\n")
    if subagent_records is not None:
        sub = os.path.join(proj, session_id, "subagents")
        os.makedirs(sub, exist_ok=True)
        with open(os.path.join(sub, "agent-abc.jsonl"), "w", encoding="utf-8") as f:
            for r in subagent_records:
                f.write(json.dumps(r, ensure_ascii=False) + "\n")
    return path


def assistant(text: str) -> dict:
    return {"type": "assistant", "message": {"content": [{"type": "text", "text": text}]}}


class TranscriptProbeTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        probe.PROJECTS_DIR = self.tmp.name

    def tearDown(self):
        self.tmp.cleanup()

    def test_find_session_file_globs_any_project_dir(self):
        path = write_session(self.tmp.name, "sid-1", [assistant("hi")])
        self.assertEqual(probe.find_session_file("sid-1"), path)
        self.assertIsNone(probe.find_session_file("sid-none"))

    def test_last_assistant_takes_final_text_block_with_timestamp(self):
        write_session(self.tmp.name, "sid-2", [
            assistant("first"),
            {"type": "user", "message": {"content": "x"}},
            {"type": "assistant", "message": {"content": [{"type": "tool_use", "name": "Bash", "input": {}}]}},
            {"type": "assistant", "timestamp": "2026-08-19T11:15:00.000Z",
             "message": {"content": [{"type": "text", "text": "You've hit your session limit · resets 11:50pm (Asia/Tokyo)"}]}},
            "not json at all",
        ])
        text, when = probe.last_assistant("sid-2")
        self.assertIn("session limit", text)
        self.assertEqual(when, datetime.datetime(2026, 8, 19, 11, 15, tzinfo=datetime.timezone.utc).timestamp())
        self.assertIn("session limit", probe.last_assistant_text("sid-2"))
        self.assertEqual(probe.last_assistant("sid-none"), ("", 0.0))

    def test_last_activity_includes_subagents(self):
        path = write_session(self.tmp.name, "sid-3", [assistant("a")], subagent_records=[assistant("b")])
        sub = os.path.join(os.path.dirname(path), "sid-3", "subagents", "agent-abc.jsonl")
        os.utime(path, (1_000, 1_000))
        os.utime(sub, (5_000, 5_000))
        self.assertEqual(probe.last_activity_epoch("sid-3"), 5_000)
        self.assertEqual(probe.last_activity_epoch("sid-missing"), 0.0)

    def test_parse_reset_epoch_same_day_and_next_day(self):
        now = datetime.datetime(2026, 8, 19, 20, 15, tzinfo=JST).timestamp()
        self.assertEqual(
            probe.parse_reset_epoch("You've hit your session limit · resets 11:50pm (Asia/Tokyo)", now),
            datetime.datetime(2026, 8, 19, 23, 50, tzinfo=JST).timestamp(),
        )
        self.assertEqual(
            probe.parse_reset_epoch("resets 4:50am (Asia/Tokyo)", now),
            datetime.datetime(2026, 8, 20, 4, 50, tzinfo=JST).timestamp(),
        )
        self.assertEqual(
            probe.parse_reset_epoch("resets 11pm (Asia/Tokyo)", now),
            datetime.datetime(2026, 8, 19, 23, 0, tzinfo=JST).timestamp(),
        )
        self.assertIsNone(probe.parse_reset_epoch("resets Aug 21 at 9pm (Asia/Tokyo)", now))
        self.assertIsNone(probe.parse_reset_epoch("no reset here", now))

    def test_detect_limit_classifies(self):
        now = datetime.datetime(2026, 8, 19, 20, 15, tzinfo=JST).timestamp()
        kind, when = probe.detect_limit("You've hit your session limit · resets 11:50pm (Asia/Tokyo)", now)
        self.assertEqual(kind, "session")
        self.assertEqual(when, datetime.datetime(2026, 8, 19, 23, 50, tzinfo=JST).timestamp())
        self.assertEqual(probe.detect_limit("You've hit your weekly limit · resets Aug 21 at 9pm", now), ("weekly", None))
        self.assertEqual(probe.detect_limit("You've hit your usage limit", now), ("session", None))
        self.assertIsNone(probe.detect_limit("all good", now))


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `python3 -m unittest test_transcript_probe -v`
Expected: FAIL（import エラー）

- [ ] **Step 3: 実装を書く**

```python
# transcript_probe.py
"""固定 session-id の Claude Code transcript から、poller が要る3点だけを読む。
Reads the three facts the poller needs from a pinned-session-id Claude Code transcript.

対話モード起動では stdout ログが無いため、最終 assistant 発話・limit・最終活動時刻は transcript が唯一の観測点。
Interactive launches have no stdout log, so the transcript is the only place to observe these.
"""
import datetime
import glob
import json
import os
import re

REAL_HOME = os.environ.get("PR_REVIEW_REAL_HOME", "/Users/sakastudio")
PROJECTS_DIR = os.environ.get("PR_REVIEW_CLAUDE_PROJECTS_DIR", os.path.join(REAL_HOME, ".claude", "projects"))
JST = datetime.timezone(datetime.timedelta(hours=9))

SESSION_LIMIT_RE = re.compile(r"hit your (session|usage) limit|rate limit|overloaded|quota", re.IGNORECASE)
WEEKLY_LIMIT_RE = re.compile(r"hit your weekly limit", re.IGNORECASE)
# "resets 11:50pm" / "resets 4:50am" / "resets 11pm"。"resets Aug 21 at 9pm"（週次）は時刻だけの形でないので不一致
# "resets 11:50pm" / "resets 4:50am" / "resets 11pm"; the weekly "resets Aug 21 at 9pm" does not match this clock-only form
RESET_CLOCK_RE = re.compile(r"resets (\d{1,2})(?::(\d{2}))?\s*([ap]m)", re.IGNORECASE)


def find_session_file(session_id: str) -> str | None:
    hits = glob.glob(os.path.join(PROJECTS_DIR, "*", f"{session_id}.jsonl"))
    return hits[0] if hits else None


def last_assistant(session_id: str) -> tuple[str, float]:
    path = find_session_file(session_id)
    if not path:
        return ("", 0.0)
    last, when = "", 0.0
    # 外部境界: transcript は書き込み途中の行や壊れた行を含みうるので1行ずつ握って読み進める
    # Boundary isolation: the transcript may hold half-written or corrupt lines; skip them line by line
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as f:
            for line in f:
                try:
                    rec = json.loads(line)
                except json.JSONDecodeError:
                    continue
                if not isinstance(rec, dict) or rec.get("type") != "assistant":
                    continue
                for block in (rec.get("message") or {}).get("content") or []:
                    if isinstance(block, dict) and block.get("type") == "text" and str(block.get("text", "")).strip():
                        last = str(block["text"])
                        when = _record_epoch(rec)
    except OSError:
        return ("", 0.0)
    return (last, when)


def _record_epoch(rec: dict) -> float:
    raw = str(rec.get("timestamp") or "")
    # 外部境界: timestamp 欠落や書式違いは 0.0（「時刻不明＝古い扱い」）に倒す
    # Boundary isolation: a missing/odd timestamp degrades to 0.0 ("unknown = old")
    try:
        return datetime.datetime.fromisoformat(raw.replace("Z", "+00:00")).timestamp()
    except ValueError:
        return 0.0


def last_assistant_text(session_id: str) -> str:
    return last_assistant(session_id)[0]


def last_activity_epoch(session_id: str) -> float:
    path = find_session_file(session_id)
    if not path:
        return 0.0
    candidates = [path] + glob.glob(os.path.join(os.path.dirname(path), session_id, "subagents", "*.jsonl"))
    latest = 0.0
    for p in candidates:
        try:
            latest = max(latest, os.path.getmtime(p))
        except OSError:
            continue
    return latest


def parse_reset_epoch(text: str, now_epoch: float) -> float | None:
    m = RESET_CLOCK_RE.search(text)
    if not m:
        return None
    hour = int(m.group(1)) % 12
    minute = int(m.group(2) or 0)
    if m.group(3).lower() == "pm":
        hour += 12
    now = datetime.datetime.fromtimestamp(now_epoch, JST)
    reset = now.replace(hour=hour, minute=minute, second=0, microsecond=0)
    # 既に過ぎた時刻なら翌日の同時刻（深夜帯の "resets 4:50am"）
    # A clock already passed today means tomorrow (the late-night "resets 4:50am" case)
    if reset <= now:
        reset += datetime.timedelta(days=1)
    return reset.timestamp()


def detect_limit(text: str, now_epoch: float) -> tuple[str, float | None] | None:
    if WEEKLY_LIMIT_RE.search(text):
        return ("weekly", None)
    if SESSION_LIMIT_RE.search(text):
        return ("session", parse_reset_epoch(text, now_epoch))
    return None
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `python3 -m unittest test_transcript_probe -v`
Expected: 5 tests PASS

---

### Task 3: poller の起動経路を cmux 対話モードへ置き換える

**Files:**
- Modify: `~/hermes-agent/data/services/pr-review/poller.py`（定数群 59〜125行、`claude_argv` 333〜340、`launch_claude` 404〜442、`start_review_session` 350〜357、`handle_adjudication` の launch 呼び出し 885〜893、`handle_applying` の launch 呼び出し 2箇所）
- Modify: `~/hermes-agent/data/services/pr-review/test_poller.py`（`LaunchClaudeTest` 18〜88行を置き換え）

**Interfaces:**
- Consumes: Task 1 `cmux_launcher`（`ping/create_workspace/close_workspace/workspace_exists`）
- Produces:
  - 定数 `UNATTENDED_PREFACE: str`、`LIMIT_CONTINUE_PROMPT: str`、`IDLE_SECONDS = 1200`、`RESET_MARGIN_SECONDS = 300`、`MAX_CONCURRENT_REVIEWS = 2`、`CMUX_DOWN_NOTIFY_INTERVAL_SECONDS = 3600`
  - `build_claude_command(prompt: str, session_id: str) -> str` — cmux へ渡すシェル1行（`env HOME=… PR_REVIEW_UNATTENDED=1 CLAUDE_CODE_PRINT_BG_WAIT_CEILING_MS=0 <claude> --session-id <id> --model <m> --dangerously-skip-permissions <UNATTENDED_PREFACE+prompt>`）
  - `launch_claude(number, prompt, pid_name, cwd, session_id) -> bool` — cmux ワークスペース作成（名前 `pr-review <N>` or `pr-apply <N>`、pid_name から決める）→ `*.workspace` に UUID 保存。`log_name`/`resume` 引数は廃止。戻り値は起動できたか
  - `workspace_path(number, pid_name) -> str`（`review.workspace` / `apply.workspace`）
  - `session_alive(session_id: str) -> bool` — `pgrep -f "session-id <id>"`
  - `start_apply_session(number) -> str`（`apply.session` に uuid）
  - `cmux_available_or_notify() -> bool` — `ping` 失敗時は1時間に1回 Discord 通知

- [ ] **Step 1: 既存 `LaunchClaudeTest` を置き換える失敗テストを書く**

```python
# test_poller.py の class LaunchClaudeTest を丸ごと以下に置換
class LaunchClaudeTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        poller.STATE_DIR = self.tmp.name
        poller.CLAUDE_BIN = "/fake/claude"
        poller.REAL_HOME = "/Users/real"
        poller.DRYRUN = False

    def tearDown(self):
        self.tmp.cleanup()

    def test_build_command_is_interactive_with_preface_and_env(self):
        cmd = poller.build_claude_command("/pr-independent-review x", "sid-1")
        self.assertNotIn(" -p ", cmd)
        self.assertIn("--session-id sid-1", cmd)
        self.assertIn("--model claude-opus-5", cmd)
        self.assertIn("--dangerously-skip-permissions", cmd)
        self.assertIn("HOME=/Users/real", cmd)
        self.assertIn("PR_REVIEW_UNATTENDED=1", cmd)
        self.assertIn("無人運用", cmd)
        self.assertIn("/pr-independent-review x", cmd)

    def test_launch_creates_workspace_and_records_uuid(self):
        with patch.object(poller.cmux, "create_workspace", return_value="02A4A452-C3FD-4781-8AE6-62DF315A1AB9") as create:
            ok = poller.launch_claude(1189, "/pr-independent-review x", "review.pid", cwd="/tmp/clone", session_id="sid-1")
        self.assertTrue(ok)
        name, cwd, command = create.call_args.args
        self.assertEqual(name, "pr-review 1189")
        self.assertEqual(cwd, "/tmp/clone")
        self.assertIn("--session-id sid-1", command)
        self.assertEqual(
            poller.read_text(poller.workspace_path(1189, "review.pid")), "02A4A452-C3FD-4781-8AE6-62DF315A1AB9"
        )

    def test_apply_launch_names_workspace_pr_apply(self):
        with patch.object(poller.cmux, "create_workspace", return_value="AAAAAAAA-0000-0000-0000-000000000000") as create:
            poller.launch_claude(1189, "/pr-adjudicated-apply 1189", "apply.pid", cwd="/tmp/slot", session_id="sid-a")
        self.assertEqual(create.call_args.args[0], "pr-apply 1189")
        self.assertEqual(poller.read_text(poller.workspace_path(1189, "apply.pid")), "AAAAAAAA-0000-0000-0000-000000000000")

    def test_launch_returns_false_when_workspace_not_created(self):
        with patch.object(poller.cmux, "create_workspace", return_value=None):
            self.assertFalse(poller.launch_claude(1189, "x", "review.pid", cwd="/tmp", session_id="sid-1"))
        self.assertIsNone(poller.read_text(poller.workspace_path(1189, "review.pid")))

    def test_session_alive_uses_pgrep(self):
        with patch.object(poller.subprocess, "run") as run:
            run.return_value.returncode = 0
            self.assertTrue(poller.session_alive("sid-1"))
            self.assertEqual(run.call_args.args[0][:2], ["/usr/bin/pgrep", "-f"])
            self.assertIn("session-id sid-1", run.call_args.args[0][2])
            run.return_value.returncode = 1
            self.assertFalse(poller.session_alive("sid-1"))

    def test_cmux_down_skips_and_notifies_once_per_hour(self):
        with patch.object(poller.cmux, "ping", return_value=False), patch.object(poller, "notify_discord") as notify:
            self.assertFalse(poller.cmux_available_or_notify())
            self.assertFalse(poller.cmux_available_or_notify())
        self.assertEqual(notify.call_count, 1)
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `python3 -m unittest test_poller.LaunchClaudeTest -v`
Expected: FAIL（`build_claude_command` 等が未定義、`launch_claude` のシグネチャ不一致）

- [ ] **Step 3: poller.py を実装する**

(a) import と定数（`RESUME_PROMPT` の直後・`RATE_LIMIT_RE` 群の置き換え）:

```python
import cmux_launcher as cmux
import transcript_probe as probe

# 無人運用宣言。プロンプト冒頭に付け、スキル側の「質問で止まるな」規律の根拠にする
# Unattended preface; prepended to every prompt so the skill applies its "never stop to ask" rule
UNATTENDED_PREFACE = (
    "【無人運用】このセッションは poller が cmux ペインでフォアグラウンド起動した無人実行である。"
    "人は見ていない前提で進め、質問で停止せず、判断が要る指摘は裁定カードへ落とすこと。"
    "人が割り込んだ場合は「止める」「続きを指示する」に限って従う。\n\n"
)
# session limit の reset 後に同じペインへ送る継続指示
# Continuation sent into the same pane once a session limit has reset
LIMIT_CONTINUE_PROMPT = (
    "session limit が reset した。同じレビューを中断地点から継続せよ。"
    "$RUNDIR/agents/*.md を点検し、報告が既にある体は再派遣しない。"
    "保持しているオーケストレータのエージェントIDへ SendMessage で「未完了分だけ続行せよ（失敗した体のみ再派遣）」と送り、"
    "最終段まで進めて findings.json を生成すること。質問で停止してはならない。"
)
IDLE_SECONDS = 1200
RESET_MARGIN_SECONDS = 300
MAX_CONCURRENT_REVIEWS = 2
CMUX_DOWN_NOTIFY_INTERVAL_SECONDS = 3600
PGREP_BIN = "/usr/bin/pgrep"
```
`RATE_LIMIT_RE`・`RATE_LIMIT_BACKOFF_SECONDS`・`LAUNCH_MARKER_PREFIX` は残してよいが、`RATE_LIMIT_RE` の利用は Task 5/6 で `probe.detect_limit` に置き換える。`RATE_LIMIT_BACKOFF_SECONDS = 1800` は「reset 時刻が読めない時のフォールバック」として残す。

(b) 起動系関数（`claude_argv` と旧 `launch_claude` を削除して以下に置換）:

```python
def build_claude_command(prompt: str, session_id: str) -> str:
    """cmux ワークスペースのシェルで実行する1行。対話モード（-p 無し）で初回プロンプトを位置引数に渡す。
    One shell line for the cmux workspace: interactive claude (no -p) with the first prompt as a positional arg."""
    env_part = shlex.join([
        "env",
        f"HOME={REAL_HOME}",
        "PR_REVIEW_UNATTENDED=1",
        "CLAUDE_CODE_PRINT_BG_WAIT_CEILING_MS=0",
    ])
    claude_part = shlex.join([
        CLAUDE_BIN, "--session-id", session_id, "--model", CLAUDE_MODEL, "--dangerously-skip-permissions",
        UNATTENDED_PREFACE + prompt,
    ])
    return f"{env_part} {claude_part}"


def workspace_path(number: int, pid_name: str) -> str:
    return os.path.join(pr_state_dir(number), pid_name.replace(".pid", ".workspace"))


def workspace_name(number: int, pid_name: str) -> str:
    return f"pr-apply {number}" if pid_name.startswith("apply") else f"pr-review {number}"


def session_alive(session_id: str) -> bool:
    if not session_id:
        return False
    # 外部境界: pgrep 不在は「生存不明＝死亡扱い」に倒す
    # Boundary isolation: a missing pgrep degrades to "unknown = dead"
    try:
        res = subprocess.run([PGREP_BIN, "-f", f"session-id {session_id}"], capture_output=True, text=True, timeout=10, check=False)
    except (OSError, subprocess.TimeoutExpired):
        return False
    return res.returncode == 0


def start_apply_session(number: int) -> str:
    session_id = str(uuid.uuid4())
    write_text(session_id_path(number, "apply.pid"), session_id)
    return session_id


def cmux_available_or_notify() -> bool:
    """cmux が応答しなければ起動を見送り、1時間に1回だけ通知する（-p フォールバックは持たない・ADR 0023）。
    Skip launches while cmux is unreachable and notify at most hourly (no headless fallback, ADR 0023)."""
    if cmux.ping():
        return True
    marker = os.path.join(STATE_DIR, "cmux-down.notified")
    last = read_float(marker, default=0.0)
    if time.time() - last >= CMUX_DOWN_NOTIFY_INTERVAL_SECONDS:
        write_text(marker, str(time.time()))
        notify_discord("⚠️ pr-review poller: cmux が応答しないため無人レビュー/applyの起動を見送っています（cmux を起動してください）")
    log("cmux 不在のため起動見送り (cmux unreachable, deferring launches)")
    return False


def launch_claude(number: int, prompt: str, pid_name: str, cwd: str, session_id: str) -> bool:
    """cmux ワークスペースで対話モード claude をフォアグラウンド起動し、UUID を記録する。dry-run では何もしない。
    Launch interactive claude in a cmux workspace and record its UUID. No-op under dry-run."""
    if DRYRUN:
        log(f"[DRYRUN] would launch in cmux: name={workspace_name(number, pid_name)} cwd={cwd} :: {build_claude_command(prompt, session_id)}")
        return True
    ws = cmux.create_workspace(workspace_name(number, pid_name), cwd, build_claude_command(prompt, session_id))
    if not ws:
        log(f"pr-{number}: cmux ワークスペース作成失敗 (failed to create cmux workspace for {pid_name})")
        return False
    write_text(workspace_path(number, pid_name), ws)
    log(f"pr-{number}: launched interactive claude in cmux workspace {ws} ({workspace_name(number, pid_name)})")
    return True


def close_workspace_of(number: int, pid_name: str) -> None:
    ws = read_text(workspace_path(number, pid_name))
    if ws and cmux.close_workspace(ws):
        log(f"pr-{number}: closed cmux workspace {ws}")
```

(c) `gh_env()` の `HOME` 上書きはそのまま（gh 用）。旧 `launch_claude` が書いていた `review.pid` はもう使わない（生存は `session_alive`）。`exit_code_path`/`read_exit_code` は Task 5 で不要になる。

(d) 呼び出し側の暫定更新（Task 4〜6 で本実装するが、この時点でテストが通るよう引数を合わせる）: `handle_waiting` の `launch_claude(...)` を `launch_claude(number, prompt=..., pid_name="review.pid", cwd=CLONE_DIR, session_id=session_id)` に、`handle_running` 内の3箇所・`handle_adjudication`・`handle_applying` の呼び出しも同様に `log_name`/`resume` を外し、apply 側は `session_id=start_apply_session(number)` を渡す。

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `python3 -m unittest test_poller -v 2>&1 | tail -30`
Expected: `LaunchClaudeTest` 6件 PASS。他クラスの一部（`ReviewSelfTerminationTest` の resume 系・`ReviewUnlimitedConcurrencyTest`）は旧挙動前提で FAIL してよい（Task 4〜6 で置換する）。FAIL 一覧をメモしておく。

---

### Task 4: レビュー同時起動を2本に制限する

**Files:**
- Modify: `~/hermes-agent/data/services/pr-review/poller.py`（`handle_waiting` 557〜595、`run_once` 1066〜1132）
- Modify: `~/hermes-agent/data/services/pr-review/test_poller.py`（`ReviewUnlimitedConcurrencyTest` 425〜444 を置換）

**Interfaces:**
- Consumes: Task 3 `launch_claude`, `session_alive`, `cmux_available_or_notify`
- Produces:
  - `handle_waiting(pr: dict, review_budget: list[int]) -> None` — `review_budget[0]` が残り起動枠。起動したら 1 減らす
  - `review_in_flight(number: int) -> bool` — `session_alive(review.session)` or `backoff_active(number)`

- [ ] **Step 1: 失敗するテストを書く**

```python
class ReviewConcurrencyCapTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        poller.STATE_DIR = self.tmp.name
        poller.DRYRUN = False
        self.patches = [
            patch.object(poller, "ensure_clone_synced", return_value=True),
            patch.object(poller, "gh_edit_labels"),
            patch.object(poller, "gh_comment"),
            patch.object(poller, "cmux_available_or_notify", return_value=True),
            patch.object(poller, "launch_claude", return_value=True),
        ]
        for p in self.patches:
            p.start()

    def tearDown(self):
        for p in self.patches:
            p.stop()
        self.tmp.cleanup()

    def _pr(self, n):
        return {"number": n, "headRefOid": "a" * 40, "labels": [{"name": poller.LABEL_WAITING}]}

    def test_only_two_waiting_prs_launch_when_none_running(self):
        budget = [poller.MAX_CONCURRENT_REVIEWS]
        for n in (1176, 1178, 1189):
            poller.handle_waiting(self._pr(n), budget)
        self.assertEqual(poller.launch_claude.call_count, 2)
        self.assertEqual(budget[0], 0)

    def test_no_budget_means_no_launch_and_no_label_change(self):
        poller.handle_waiting(self._pr(1189), [0])
        poller.launch_claude.assert_not_called()
        poller.gh_edit_labels.assert_not_called()

    def test_run_once_budget_subtracts_in_flight_reviews(self):
        running = {"number": 1, "headRefOid": "a" * 40, "labels": [{"name": poller.LABEL_RUNNING}]}
        waiting = [self._pr(2), self._pr(3)]
        with patch.object(poller, "fetch_open_prs", return_value=[running] + waiting), \
             patch.object(poller, "handle_running"), \
             patch.object(poller, "review_in_flight", return_value=True), \
             patch.object(poller, "scan_live_apply_slots", return_value=set()):
            poller.run_once()
        self.assertEqual(poller.launch_claude.call_count, 1)

    def test_cmux_down_launches_nothing(self):
        with patch.object(poller, "cmux_available_or_notify", return_value=False):
            poller.handle_waiting(self._pr(1189), [2])
        poller.launch_claude.assert_not_called()
        poller.gh_edit_labels.assert_not_called()
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `python3 -m unittest test_poller.ReviewConcurrencyCapTest -v`
Expected: FAIL（`handle_waiting` が第2引数を受けない／`review_in_flight` 未定義）

- [ ] **Step 3: 実装する**

`handle_waiting` を以下に置換（docstring の「同時起動数に制限は設けない」も削除）:

```python
def review_in_flight(number: int) -> bool:
    """実行中ラベルのPRが起動枠を占有しているか（生存 or limit待ち）。
    Whether a running-labelled PR still occupies a review slot (alive or waiting for a limit reset)."""
    session_id = read_text(session_id_path(number, "review.pid"), default="") or ""
    return session_alive(session_id) or backoff_active(number)


def handle_waiting(pr: dict, review_budget: list[int]) -> None:
    """レビュージョブを起動する。同時起動は MAX_CONCURRENT_REVIEWS 本まで（ユーザー裁定 2026-08-20、1本は昼間に遅すぎる）。
    Launch the review job. At most MAX_CONCURRENT_REVIEWS run at once (user adjudication 2026-08-20)."""
    number = pr["number"]
    sha7 = pr["headRefOid"][:7]
    if review_budget[0] <= 0:
        log(f"pr-{number}: レビュー同時起動上限のため見送り (review concurrency cap reached, deferring)")
        return
    if not ensure_clone_synced():
        log(f"pr-{number}: メインクローンが未同期のため起動見送り、待ちのまま (clone not in sync, staying in 待ち)")
        return
    if not cmux_available_or_notify():
        return
    if DRYRUN:
        log(f"[DRYRUN] pr-{number}: would transition 待ち -> 実行中, comment, launch review (head={sha7})")
        review_budget[0] -= 1
        return

    session_id = start_review_session(number)
    if not launch_claude(
        number,
        prompt=f"/pr-independent-review https://github.com/{REPO}/pull/{number}",
        pid_name="review.pid",
        cwd=CLONE_DIR,
        session_id=session_id,
    ):
        return
    review_budget[0] -= 1
    gh_edit_labels(number, add=[LABEL_RUNNING], remove=[LABEL_WAITING], description=f"pr-{number} 待ち->実行中")
    gh_comment(number, f"🤖 独立レビュー開始（head: {sha7}）", description=f"pr-{number} 開始コメント")
    set_marker(number, "review_started_comment")
    write_text(os.path.join(pr_state_dir(number), "review.retry"), "0")
    write_text(os.path.join(pr_state_dir(number), "review.started"), str(time.time()))
    log(f"pr-{number}: 待ち -> 実行中 (waiting -> running)")
```

（起動に失敗したらラベルを動かさない順序にしたのは、ワークスペース作成失敗で「実行中なのに何も走っていない」状態を作らないため。）

`run_once` の変更:

```python
    # レビュー起動枠: 実行中（生存 or limit待ち）の本数を引いた残り
    # Review budget: cap minus reviews still in flight (alive or waiting for a limit reset)
    in_flight = sum(1 for pr, label in targets if label == LABEL_RUNNING and review_in_flight(pr["number"]))
    review_budget = [max(0, MAX_CONCURRENT_REVIEWS - in_flight)]
```
を `occupied_slots |= scan_live_apply_slots()` の直後に置き、`process()` 内の `handle_waiting(pr)` を `handle_waiting(pr, review_budget)` にする。

旧 `ReviewUnlimitedConcurrencyTest` は削除する。

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `python3 -m unittest test_poller.ReviewConcurrencyCapTest test_poller.LaunchClaudeTest -v`
Expected: PASS

---

### Task 5: handle_running を transcript ベースの検知へ書き換える

**Files:**
- Modify: `~/hermes-agent/data/services/pr-review/poller.py`（`rate_limited`/`backoff_active`/`set_backoff` 266〜282、`handle_running` 635〜790、`enter_failed` 623〜634）
- Modify: `~/hermes-agent/data/services/pr-review/test_poller.py`（`ReviewSelfTerminationTest` 184〜381 を置換）

**Interfaces:**
- Consumes: Task 2 `probe.last_assistant_text/last_activity_epoch/detect_limit`、Task 1 `cmux.capture_tail/send_text/close_workspace`、Task 3 `session_alive/launch_claude/close_workspace_of`
- Produces:
  - `observed_limit(number: int, pid_name: str, since_epoch: float) -> tuple[str, float | None] | None` — transcript 最終発話（レコード時刻が `since_epoch` より新しいものだけ）→ 無ければ capture-pane 末尾20行で `probe.detect_limit`。`since_epoch` は直近の起動/継続送信時刻（`*.started`）で、継続送信後に残る古い limit 文言を再検知しないため
  - `set_backoff_until(number: int, reset_epoch: float | None) -> None` — `reset+RESET_MARGIN_SECONDS`。None、または reset が6時間より先（5h枠ではあり得ない＝翌日誤解決）なら `now+RATE_LIMIT_BACKOFF_SECONDS`
  - `failure_excerpt(number: int, pid_name: str) -> str` — transcript 最終発話（無ければ capture-pane 末尾20行）
  - `enter_failed(pr, marker_name, pid_name, extra)` — `log_name` を `pid_name` に変更し `failure_excerpt` を使う

- [ ] **Step 1: 失敗するテストを書く（`ReviewSelfTerminationTest` を丸ごと置換）**

```python
class ReviewRunningTest(unittest.TestCase):
    """実行中PRの分岐: findings→裁定待ち / limit→reset待ち→継続送信 / weekly→失敗 / idle→RESUME送信 / 死亡→retry"""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        poller.STATE_DIR = self.tmp.name
        poller.RUNDIR_BASE = os.path.join(self.tmp.name, "runs")
        os.makedirs(os.path.join(poller.RUNDIR_BASE, "pr-7"), exist_ok=True)
        poller.DRYRUN = False
        poller.write_text(poller.session_id_path(7, "review.pid"), "sid-7")
        poller.write_text(poller.workspace_path(7, "review.pid"), "02A4A452-C3FD-4781-8AE6-62DF315A1AB9")
        poller.write_text(os.path.join(poller.pr_state_dir(7), "review.started"), str(time.time() - 60))
        poller.write_text(os.path.join(poller.pr_state_dir(7), "review.retry"), "0")
        poller.write_text(os.path.join(poller.pr_state_dir(7), "review.resume"), "0")
        poller.set_marker(7, "review_started_comment")
        self.pr = {"number": 7, "headRefOid": "h" * 40, "labels": [{"name": poller.LABEL_RUNNING}]}
        self.patches = {
            "labels": patch.object(poller, "gh_edit_labels"),
            "comment": patch.object(poller, "gh_comment"),
            "launch": patch.object(poller, "launch_claude", return_value=True),
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

    def _limit_now(self, text):
        self.m["last"].return_value = (text, time.time())

    def _write_findings(self):
        path = os.path.join(poller.rundir(7), "findings.json")
        with open(path, "w", encoding="utf-8") as f:
            json.dump({"head": "h" * 40, "findings": []}, f)

    def test_findings_closes_workspace_and_enters_adjudication(self):
        self._write_findings()
        with patch.object(poller, "enter_adjudication_wait") as enter:
            self.assertFalse(poller.handle_running(self.pr))
        enter.assert_called_once()
        self.m["close"].assert_called_once_with("02A4A452-C3FD-4781-8AE6-62DF315A1AB9")

    def test_alive_and_active_keeps_waiting(self):
        self.assertTrue(poller.handle_running(self.pr))
        self.m["send"].assert_not_called()
        self.m["launch"].assert_not_called()

    def test_session_limit_sets_backoff_until_reset_then_sends_continue(self):
        self._limit_now("You've hit your session limit · resets 11:50pm (Asia/Tokyo)")
        self.assertTrue(poller.handle_running(self.pr))
        until = poller.read_float(os.path.join(poller.pr_state_dir(7), "ratelimit.until"))
        self.assertGreater(until, time.time() + 60)
        self.m["send"].assert_not_called()
        # reset 到来後の tick で同じペインへ継続指示
        poller.write_text(os.path.join(poller.pr_state_dir(7), "ratelimit.until"), str(time.time() - 1))
        self.assertTrue(poller.handle_running(self.pr))
        ws, text = self.m["send"].call_args.args
        self.assertEqual(ws, "02A4A452-C3FD-4781-8AE6-62DF315A1AB9")
        self.assertIn("SendMessage", text)
        self.assertEqual(poller.read_text(os.path.join(poller.pr_state_dir(7), "review.retry")), "0")
        # 継続送信後、古い limit 文言が残っていても再検知しない（started が更新されている）
        self.assertTrue(poller.handle_running(self.pr))
        self.assertEqual(self.m["send"].call_count, 1)
        self.assertFalse(poller.backoff_active(7))

    def test_reset_resolved_too_far_ahead_falls_back_to_1800s(self):
        far = time.time() + 7 * 3600
        poller.set_backoff_until(7, far)
        until = poller.read_float(os.path.join(poller.pr_state_dir(7), "ratelimit.until"))
        self.assertAlmostEqual(until, time.time() + poller.RATE_LIMIT_BACKOFF_SECONDS, delta=5)

    def test_session_limit_without_clock_falls_back_to_1800s(self):
        self._limit_now("You've hit your usage limit")
        poller.handle_running(self.pr)
        until = poller.read_float(os.path.join(poller.pr_state_dir(7), "ratelimit.until"))
        self.assertAlmostEqual(until, time.time() + poller.RATE_LIMIT_BACKOFF_SECONDS, delta=5)

    def test_limit_seen_only_on_screen_counts_too(self):
        self.m["capture"].return_value = "...\nYou've hit your session limit · resets 4:50am (Asia/Tokyo)\n"
        poller.handle_running(self.pr)
        self.assertGreater(poller.read_float(os.path.join(poller.pr_state_dir(7), "ratelimit.until")), time.time())

    def test_weekly_limit_fails_and_keeps_workspace(self):
        self._limit_now("You've hit your weekly limit · resets Aug 21 at 9pm (Asia/Tokyo)")
        self.assertFalse(poller.handle_running(self.pr))
        self.m["labels"].assert_called()
        self.assertIn(poller.LABEL_FAILED, self.m["labels"].call_args.kwargs["add"])
        self.assertIn("週次上限", self.m["comment"].call_args.args[1])
        self.m["close"].assert_not_called()

    def test_idle_alive_sends_resume_once_then_fails(self):
        self.m["activity"].return_value = time.time() - poller.IDLE_SECONDS - 1
        self.assertTrue(poller.handle_running(self.pr))
        self.assertIn("findings.json", self.m["send"].call_args.args[1])
        self.assertEqual(poller.read_text(os.path.join(poller.pr_state_dir(7), "review.resume")), "1")
        self.m["activity"].return_value = time.time() - poller.IDLE_SECONDS - 1
        self.assertFalse(poller.handle_running(self.pr))
        self.assertIn(poller.LABEL_FAILED, self.m["labels"].call_args.kwargs["add"])

    def test_resume_sent_refreshes_started_so_idle_clock_restarts(self):
        self.m["activity"].return_value = time.time() - poller.IDLE_SECONDS - 1
        before = poller.read_float(os.path.join(poller.pr_state_dir(7), "review.started"))
        poller.handle_running(self.pr)
        after = poller.read_float(os.path.join(poller.pr_state_dir(7), "review.started"))
        self.assertGreater(after, before)

    def test_dead_without_limit_retries_with_new_session_and_workspace(self):
        self.m["alive"].return_value = False
        self.assertTrue(poller.handle_running(self.pr))
        self.m["close"].assert_called_once()
        self.m["launch"].assert_called_once()
        self.assertNotEqual(poller.read_text(poller.session_id_path(7, "review.pid")), "sid-7")
        self.assertEqual(poller.read_text(os.path.join(poller.pr_state_dir(7), "review.retry")), "1")

    def test_dead_after_retry_budget_fails(self):
        self.m["alive"].return_value = False
        poller.write_text(os.path.join(poller.pr_state_dir(7), "review.retry"), str(poller.MAX_REVIEW_RETRY))
        self.assertFalse(poller.handle_running(self.pr))
        self.assertIn(poller.LABEL_FAILED, self.m["labels"].call_args.kwargs["add"])

    def test_abort_json_beats_everything(self):
        with open(os.path.join(poller.rundir(7), "abort.json"), "w", encoding="utf-8") as f:
            json.dump({"reason": "fail-closed"}, f)
        self.m["alive"].return_value = False
        self.assertFalse(poller.handle_running(self.pr))
        self.assertIn("fail-closed", self.m["comment"].call_args.args[1])
        self.m["launch"].assert_not_called()

    def test_failure_excerpt_prefers_transcript_then_screen(self):
        self.m["last"].return_value = ("最後の発話", time.time())
        self.assertEqual(poller.failure_excerpt(7, "review.pid"), "最後の発話")
        self.m["last"].return_value = ("", 0.0)
        self.m["capture"].return_value = "screen tail"
        self.assertEqual(poller.failure_excerpt(7, "review.pid"), "screen tail")
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `python3 -m unittest test_poller.ReviewRunningTest -v`
Expected: FAIL（新関数未定義・旧分岐）

- [ ] **Step 3: 実装する**

補助関数（`rate_limited`/`set_backoff` を置換。`backoff_active` はそのまま）:

```python
def observed_limit(number: int, pid_name: str, since_epoch: float) -> tuple[str, float | None] | None:
    """transcript の最終発話（since_epoch より新しいもの）を第一に、無ければスクリーン末尾で limit を判定する。
    対話モードでは文言が transcript に残らない可能性があるため両方見る。継続送信後に残る古い limit 文言は since で弾く。
    Check the transcript's last utterance (newer than since_epoch) first, then the screen tail; interactive mode may
    not log the limit text, and the since gate keeps a stale limit line from re-arming after a continuation."""
    now = time.time()
    session_id = read_text(session_id_path(number, pid_name), default="") or ""
    text, when = probe.last_assistant(session_id)
    if when >= since_epoch:
        hit = probe.detect_limit(text, now)
        if hit:
            return hit
    if when >= since_epoch and text.strip():
        # 直近に transcript 側で発話が進んでいるならスクリーンは見ない（古い画面文言の誤検知を避ける）
        # If the transcript has moved on since the last launch/continuation, skip the screen (stale text risk)
        return None
    ws = read_text(workspace_path(number, pid_name), default="") or ""
    return probe.detect_limit(cmux.capture_tail(ws, 20), now) if ws else None


MAX_PLAUSIBLE_RESET_SECONDS = 6 * 3600


def set_backoff_until(number: int, reset_epoch: float | None) -> None:
    """reset が読めない・6時間超先（5h枠ではあり得ない＝翌日へ誤解決）なら固定バックオフへ倒す。
    Fall back to the fixed backoff when the reset is unreadable or implausibly far (misresolved to tomorrow)."""
    now = time.time()
    if reset_epoch is None or reset_epoch - now > MAX_PLAUSIBLE_RESET_SECONDS:
        until = now + RATE_LIMIT_BACKOFF_SECONDS
    else:
        until = reset_epoch + RESET_MARGIN_SECONDS
    write_text(os.path.join(pr_state_dir(number), "ratelimit.until"), str(until))


def failure_excerpt(number: int, pid_name: str) -> str:
    session_id = read_text(session_id_path(number, pid_name), default="") or ""
    text = probe.last_assistant_text(session_id).strip()
    if text:
        return text[-1500:]
    ws = read_text(workspace_path(number, pid_name), default="") or ""
    return (cmux.capture_tail(ws, 20) if ws else "") or "(抜粋なし / no excerpt)"


def enter_failed(pr: dict, marker_name: str, pid_name: str, extra: str = "") -> None:
    number = pr["number"]
    current_labels = label_names(pr)
    remove = [l for l in (LABEL_RUNNING, LABEL_APPLYING) if l in current_labels]
    gh_edit_labels(number, add=[LABEL_FAILED], remove=remove, description=f"pr-{number} ->失敗")
    body = f"❌ 独立レビューパイプライン失敗{extra}\n\n```\n{failure_excerpt(number, pid_name)}\n```"
    gh_comment(number, body, description=f"pr-{number} 失敗コメント")
    set_marker(number, marker_name)
    log(f"pr-{number}: -> 失敗 ({marker_name})")
```

`handle_running` の本体（findings 検出部の後ろを以下に置換。findings 検出の成功分岐には `close_workspace_of(number, "review.pid")` を `enter_adjudication_wait(pr)` の直前に足す）:

```python
    session_id = read_text(session_id_path(number, "review.pid"), default="") or ""
    alive = session_alive(session_id)

    # 中止申告は最優先（limit 推定に押し切られると fail-closed が無効化される）
    # The abort notice outranks everything; a limit heuristic must not override fail-closed
    aborted = read_abort(number, started_at)
    if aborted is not None:
        if DRYRUN:
            log(f"[DRYRUN] pr-{number}: レビューが中止を申告、失敗へ遷移予定")
            return False
        enter_failed(pr, marker_name="failed_comment_review", pid_name="review.pid", extra=f"（レビューが中止を申告: {aborted}）")
        return False

    # limit: weekly は人を呼ぶ。session は reset まで待ち、同じペインへ継続指示を送る（ユーザー裁定 2026-08-20）
    # Limits: weekly summons a human; session waits for the reset then continues in the same pane
    limit = observed_limit(number, "review.pid", started_at)
    if limit and limit[0] == "weekly":
        if DRYRUN:
            log(f"[DRYRUN] pr-{number}: 週次上限検知、失敗へ遷移予定")
            return False
        enter_failed(pr, marker_name="failed_comment_review", pid_name="review.pid",
                     extra="（週次上限に到達。アカウント切替後にラベルを戻して再開してください）")
        return False
    if limit and limit[0] == "session":
        if not backoff_active(number) and not has_marker(number, "limit_wait_armed"):
            set_backoff_until(number, limit[1])
            set_marker(number, "limit_wait_armed")
            log(f"pr-{number}: session limit 検知、reset まで待機 (session limit; waiting for reset)")
            return True
        if backoff_active(number):
            log(f"pr-{number}: session limit reset 待ち (waiting for limit reset)")
            return True
        # reset 到来。プロセスが生きていれば同じペインへ継続、死んでいれば下の死亡経路へ
        # Reset reached: continue in the same pane if alive, otherwise fall through to the death path
        os.remove(marker_path(number, "limit_wait_armed"))
        if alive:
            ws = read_text(workspace_path(number, "review.pid"), default="") or ""
            if DRYRUN:
                log(f"[DRYRUN] pr-{number}: reset 到来、継続指示送信予定")
                return True
            if cmux.send_text(ws, LIMIT_CONTINUE_PROMPT):
                write_text(started_path, str(time.time()))
                log(f"pr-{number}: reset 到来、同一セッションへ継続指示を送信 (limit reset; continuation sent)")
                return True
            log(f"pr-{number}: 継続指示の送信失敗 (failed to send continuation)")

    if alive:
        idle_for = time.time() - max(probe.last_activity_epoch(session_id), started_at)
        if idle_for < IDLE_SECONDS:
            log(f"pr-{number}: レビュー実行中、findings未検出 (still running, no findings yet)")
            return True
        # 生きているのに動いていない＝自壊相当。同じペインへ RESUME を1回だけ送る（予算は従来どおり）
        # Alive but inactive = self-destruct equivalent; send RESUME into the same pane once (same budget)
        resume_path = os.path.join(sdir, "review.resume")
        resume_count = read_int(resume_path, default=0)
        if resume_count < MAX_REVIEW_RESUME:
            ws = read_text(workspace_path(number, "review.pid"), default="") or ""
            if DRYRUN:
                log(f"[DRYRUN] pr-{number}: idle 検知、RESUME 送信予定")
                return True
            write_text(resume_path, str(resume_count + 1))
            write_text(started_path, str(time.time()))
            cmux.send_text(ws, RESUME_PROMPT)
            log(f"pr-{number}: idle {int(idle_for)}s、同一セッションへ RESUME 送信 ({resume_count + 1}/{MAX_REVIEW_RESUME})")
            return True
        if DRYRUN:
            log(f"[DRYRUN] pr-{number}: resume 上限到達、失敗へ遷移予定")
            return False
        enter_failed(pr, marker_name="failed_comment_review", pid_name="review.pid",
                     extra="（レビューが停止・findings未生成: resume 1回でも回収できず）")
        return False

    # 死んでいて findings も limit も無い: 新セッション・新ワークスペースで retry
    # Dead with neither findings nor a limit: retry with a fresh session and workspace
    retry_path = os.path.join(sdir, "review.retry")
    retry_count = read_int(retry_path, default=0)
    if retry_count < MAX_REVIEW_RETRY:
        if DRYRUN:
            log(f"[DRYRUN] pr-{number}: レビュープロセス死亡、再起動予定 (attempt {retry_count + 1})")
            return True
        close_workspace_of(number, "review.pid")
        write_text(retry_path, str(retry_count + 1))
        write_text(started_path, str(time.time()))
        log(f"pr-{number}: レビュープロセス死亡、再起動 (retry attempt {retry_count + 1}/{MAX_REVIEW_RETRY})")
        launch_claude(number, prompt=f"/pr-independent-review https://github.com/{REPO}/pull/{number}",
                      pid_name="review.pid", cwd=CLONE_DIR, session_id=start_review_session(number))
        return True

    if DRYRUN:
        log(f"[DRYRUN] pr-{number}: リトライ上限到達、失敗へ遷移予定")
        return False
    enter_failed(pr, marker_name="failed_comment_review", pid_name="review.pid", extra="（レビューフェーズ・リトライ上限到達）")
    return False
```

`exit_code_path`/`read_exit_code` と「exit==0 の自力終了」分岐は削除する（対話モードではターン終了でプロセスが死なない。idle 検知が代替）。`start_review_session` は `review.resume` を 0 に戻す挙動を維持。

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `python3 -m unittest test_poller.ReviewRunningTest -v`
Expected: 13 tests PASS

---

### Task 6: handle_applying を同じ検知へ揃える

**Files:**
- Modify: `~/hermes-agent/data/services/pr-review/poller.py`（`handle_adjudication` の launch 885〜893、`handle_applying` 911〜1030、`scan_live_apply_slots` 194〜211）
- Modify: `~/hermes-agent/data/services/pr-review/test_poller.py`（`ApplyProcessDeathTest` 89〜128・`ApplySlotPoolTest` 129〜183 を更新）

**Interfaces:**
- Consumes: Task 3 `start_apply_session/launch_claude/session_alive/close_workspace_of`、Task 5 `observed_limit/set_backoff_until/enter_failed(pid_name=...)`
- Produces: `scan_live_apply_slots()` が `apply.session` の生存で判定する（`apply.pid` ではなく）

- [ ] **Step 1: 失敗するテストを書く（`ApplyProcessDeathTest`・`ApplySlotPoolTest` を以下で置換）**

```python
class ApplyRunningTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        poller.STATE_DIR = self.tmp.name
        poller.RUNDIR_BASE = os.path.join(self.tmp.name, "runs")
        os.makedirs(os.path.join(poller.RUNDIR_BASE, "pr-9"), exist_ok=True)
        poller.APPLY_SLOT_DIRS = [os.path.join(self.tmp.name, "slot1"), os.path.join(self.tmp.name, "slot2")]
        for s in poller.APPLY_SLOT_DIRS:
            os.makedirs(s, exist_ok=True)
        poller.DRYRUN = False
        poller.write_text(poller.session_id_path(9, "apply.pid"), "sid-a9")
        poller.write_text(poller.workspace_path(9, "apply.pid"), "BBBBBBBB-0000-0000-0000-000000000000")
        poller.set_apply_slot(9, poller.APPLY_SLOT_DIRS[0])
        poller.write_text(os.path.join(poller.pr_state_dir(9), "apply.retry"), "0")
        poller.write_text(os.path.join(poller.pr_state_dir(9), "apply.started"), str(time.time() - 60))
        poller.set_marker(9, "apply_start_comment")
        self.pr = {"number": 9, "headRefOid": "h" * 40, "labels": [{"name": poller.LABEL_APPLYING}]}
        self.patches = {
            "labels": patch.object(poller, "gh_edit_labels"),
            "comment": patch.object(poller, "gh_comment"),
            "launch": patch.object(poller, "launch_claude", return_value=True),
            "send": patch.object(poller.cmux, "send_text", return_value=True),
            "close": patch.object(poller.cmux, "close_workspace", return_value=True),
            "capture": patch.object(poller.cmux, "capture_tail", return_value=""),
            "alive": patch.object(poller, "session_alive", return_value=True),
            "last": patch.object(poller.probe, "last_assistant", return_value=("", 0.0)),
        }
        self.m = {k: p.start() for k, p in self.patches.items()}

    def tearDown(self):
        for p in self.patches.values():
            p.stop()
        self.tmp.cleanup()

    def test_success_result_closes_workspace_and_marks_done(self):
        with open(os.path.join(poller.rundir(9), "apply-result.json"), "w", encoding="utf-8") as f:
            json.dump({"status": "success", "summary": "ok"}, f)
        self.assertFalse(poller.handle_applying(self.pr))
        self.m["close"].assert_called_once_with("BBBBBBBB-0000-0000-0000-000000000000")
        self.assertIn(poller.LABEL_DONE, self.m["labels"].call_args.kwargs["add"])

    def test_session_limit_waits_then_sends_continue_and_keeps_slot(self):
        self.m["last"].return_value = ("You've hit your session limit · resets 11:50pm (Asia/Tokyo)", time.time())
        self.assertTrue(poller.handle_applying(self.pr))
        poller.write_text(os.path.join(poller.pr_state_dir(9), "ratelimit.until"), str(time.time() - 1))
        self.assertTrue(poller.handle_applying(self.pr))
        self.assertEqual(self.m["send"].call_args.args[0], "BBBBBBBB-0000-0000-0000-000000000000")

    def test_weekly_limit_fails(self):
        self.m["last"].return_value = ("You've hit your weekly limit · resets Aug 24 at 1am", time.time())
        self.assertFalse(poller.handle_applying(self.pr))
        self.assertIn(poller.LABEL_FAILED, self.m["labels"].call_args.kwargs["add"])

    def test_first_death_relaunches_with_new_session_then_second_death_fails(self):
        self.m["alive"].return_value = False
        self.assertTrue(poller.handle_applying(self.pr))
        self.m["close"].assert_called_once()
        self.assertEqual(self.m["launch"].call_args.kwargs["cwd"], poller.APPLY_SLOT_DIRS[0])
        self.assertNotEqual(poller.read_text(poller.session_id_path(9, "apply.pid")), "sid-a9")
        self.assertFalse(poller.handle_applying(self.pr))
        self.assertIn(poller.LABEL_FAILED, self.m["labels"].call_args.kwargs["add"])


class ApplySlotPoolTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        poller.STATE_DIR = self.tmp.name
        poller.RUNDIR_BASE = os.path.join(self.tmp.name, "runs")
        poller.APPLY_SLOT_DIRS = [os.path.join(self.tmp.name, "slot1"), os.path.join(self.tmp.name, "slot2")]
        for s in poller.APPLY_SLOT_DIRS:
            os.makedirs(s, exist_ok=True)
        poller.DRYRUN = False
        os.makedirs(os.path.join(poller.RUNDIR_BASE, "pr-5"), exist_ok=True)
        with open(os.path.join(poller.RUNDIR_BASE, "pr-5", "adjudications.json"), "w", encoding="utf-8") as f:
            json.dump({"findings": [{"id": "F1", "decision": "A"}]}, f)
        poller.set_marker(5, "adjudication_wait_comment")
        self.pr = {"number": 5, "headRefOid": "h" * 40, "labels": [{"name": poller.LABEL_ADJUDICATION}]}
        self.patches = [
            patch.object(poller, "gh_edit_labels"), patch.object(poller, "gh_comment"),
            patch.object(poller, "launch_claude", return_value=True),
            patch.object(poller, "cmux_available_or_notify", return_value=True),
            patch.object(poller, "count_adjudications", return_value=(1, 0, 0)),
        ]
        for p in self.patches:
            p.start()

    def tearDown(self):
        for p in self.patches:
            p.stop()
        self.tmp.cleanup()

    def test_launch_consumes_slot_records_assignment_and_session(self):
        free = list(poller.APPLY_SLOT_DIRS)
        self.assertTrue(poller.handle_adjudication(self.pr, free))
        self.assertEqual(free, [poller.APPLY_SLOT_DIRS[1]])
        self.assertEqual(poller.apply_slot(5), poller.APPLY_SLOT_DIRS[0])
        self.assertTrue(poller.read_text(poller.session_id_path(5, "apply.pid")))
        self.assertEqual(poller.launch_claude.call_args.kwargs["cwd"], poller.APPLY_SLOT_DIRS[0])

    def test_no_free_slot_defers_without_side_effects(self):
        self.assertFalse(poller.handle_adjudication(self.pr, []))
        poller.launch_claude.assert_not_called()

    def test_cmux_down_defers_without_consuming_slot(self):
        with patch.object(poller, "cmux_available_or_notify", return_value=False):
            free = list(poller.APPLY_SLOT_DIRS)
            self.assertFalse(poller.handle_adjudication(self.pr, free))
        self.assertEqual(len(free), 2)
        poller.launch_claude.assert_not_called()

    def test_scan_live_apply_slots_uses_session_alive(self):
        poller.write_text(poller.session_id_path(11, "apply.pid"), "sid-11")
        poller.set_apply_slot(11, poller.APPLY_SLOT_DIRS[1])
        with patch.object(poller, "session_alive", side_effect=lambda s: s == "sid-11"):
            self.assertEqual(poller.scan_live_apply_slots(), {poller.APPLY_SLOT_DIRS[1]})
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `python3 -m unittest test_poller.ApplyRunningTest test_poller.ApplySlotPoolTest -v`
Expected: FAIL

- [ ] **Step 3: 実装する**

`handle_adjudication` の起動部分:

```python
    if not free_slots:
        log(f"pr-{number}: apply起動待ち。空きapplyスロットなしのため見送り (apply deferred: no free apply slot)")
        return False
    if not cmux_available_or_notify():
        return False
    slot = free_slots.pop(0)
    if DRYRUN:
        log(f"[DRYRUN] pr-{number}: 裁定完了・採用{adopted}件、対応中へ遷移しapply起動予定 (slot={slot})")
        return True
    stale_result = os.path.join(rundir(number), "apply-result.json")
    if os.path.exists(stale_result):
        os.remove(stale_result)
        log(f"pr-{number}: 前回のapply-result.jsonを削除 (removed stale apply-result.json)")
    session_id = start_apply_session(number)
    if not launch_claude(number, prompt=f"/pr-adjudicated-apply {number}", pid_name="apply.pid", cwd=slot, session_id=session_id):
        free_slots.insert(0, slot)
        return False
    enter_applying(pr, adopted, rejected, auto)
    set_apply_slot(number, slot)
    write_text(os.path.join(pr_state_dir(number), "apply.retry"), "0")
    write_text(os.path.join(pr_state_dir(number), "apply.started"), str(time.time()))
    log(f"pr-{number}: 裁定待ち -> 対応中 (adjudication-wait -> applying, adopted={adopted}, slot={slot})")
    return True
```

`scan_live_apply_slots` 内の `read_pid_alive(os.path.join(sdir, "apply.pid"))` を `session_alive(read_text(os.path.join(sdir, "apply.session"), default="") or "")` に置換。

`handle_applying` の result 成功分岐に `close_workspace_of(number, "apply.pid")` を `gh_edit_labels(...)` の直前に足し、失敗分岐は `enter_failed(pr, marker_name="failed_comment_apply", pid_name="apply.pid", extra=...)` に。`read_pid_alive` 以降を以下に置換:

```python
    session_id = read_text(session_id_path(number, "apply.pid"), default="") or ""
    alive = session_alive(session_id)
    limit = observed_limit(number, "apply.pid", read_float(os.path.join(sdir, "apply.started"), default=0.0))
    if limit and limit[0] == "weekly":
        if DRYRUN:
            log(f"[DRYRUN] pr-{number}: apply 週次上限検知、失敗へ遷移予定")
            return False
        enter_failed(pr, marker_name="failed_comment_apply", pid_name="apply.pid",
                     extra="（適用フェーズ・週次上限に到達。アカウント切替後にラベルを戻して再開してください）")
        return False
    if limit and limit[0] == "session":
        if not backoff_active(number) and not has_marker(number, "apply_limit_wait_armed"):
            set_backoff_until(number, limit[1])
            set_marker(number, "apply_limit_wait_armed")
            log(f"pr-{number}: apply session limit 検知、reset まで待機 (apply session limit; waiting for reset)")
            return True
        if backoff_active(number):
            log(f"pr-{number}: apply session limit reset 待ち (apply waiting for limit reset)")
            return True
        os.remove(marker_path(number, "apply_limit_wait_armed"))
        if alive:
            ws = read_text(workspace_path(number, "apply.pid"), default="") or ""
            if DRYRUN:
                log(f"[DRYRUN] pr-{number}: apply reset 到来、継続指示送信予定")
                return True
            if cmux.send_text(ws, "session limit が reset した。同じ apply を中断地点から継続し、apply-result.json を書いて終えること。質問で停止してはならない。"):
                write_text(os.path.join(sdir, "apply.started"), str(time.time()))
                log(f"pr-{number}: apply reset 到来、同一セッションへ継続指示を送信 (apply continuation sent)")
                return True
    if alive:
        log(f"pr-{number}: apply実行中、結果未検出 (apply still running, no result yet)")
        return True

    retry_path = os.path.join(sdir, "apply.retry")
    retry_count = read_int(retry_path, default=0)
    if retry_count < MAX_APPLY_RETRY:
        if DRYRUN:
            log(f"[DRYRUN] pr-{number}: applyプロセス死亡、再起動予定 (attempt {retry_count + 1})")
            return True
        close_workspace_of(number, "apply.pid")
        write_text(retry_path, str(retry_count + 1))
        write_text(os.path.join(sdir, "apply.started"), str(time.time()))
        log(f"pr-{number}: applyプロセス死亡、再起動 (retry attempt {retry_count + 1}/{MAX_APPLY_RETRY})")
        launch_claude(number, prompt=f"/pr-adjudicated-apply {number}", pid_name="apply.pid", cwd=slot, session_id=start_apply_session(number))
        return True

    if DRYRUN:
        log(f"[DRYRUN] pr-{number}: applyリトライ上限到達、失敗へ遷移予定")
        return False
    enter_failed(pr, marker_name="failed_comment_apply", pid_name="apply.pid", extra="（適用フェーズ・プロセス死亡・リトライ上限到達）")
    return False
```

（apply は従来どおり idle 検知を持たない: apply セッションは長時間 subagent を待たず、死亡＝retry で十分。）

- [ ] **Step 4: 全テストを実行して通ることを確認する**

Run: `python3 -m unittest test_poller test_cmux_launcher test_transcript_probe -v 2>&1 | tail -15`
Expected: `OK`（`CloneSyncGateTest` 等の既存テストも含め全緑。`test_out_of_sync_clone_defers_launch_and_keeps_label` は `handle_waiting(pr, [2])` 形に引数を直す）

---

### Task 7: 旧 pid/log 経路の撤去と README 更新

**Files:**
- Modify: `~/hermes-agent/data/services/pr-review/poller.py`（`read_pid_alive`/`pid_alive`/`tail_lines`/`exit_code_path`/`read_exit_code`/`LAUNCH_MARKER_PREFIX`/`RATE_LIMIT_RE` の未参照残骸を削除）
- Modify: `~/hermes-agent/data/services/pr-review/README.md`

- [ ] **Step 1: 未参照を機械的に確認する**

Run: `cd ~/hermes-agent/data/services/pr-review && for f in read_pid_alive pid_alive tail_lines exit_code_path read_exit_code LAUNCH_MARKER_PREFIX RATE_LIMIT_RE claude_argv rate_limited set_backoff; do echo "$f: $(grep -c "\b$f\b" poller.py)"; done`
Expected: 定義行のみ（1）か 0 のもの → 削除対象

- [ ] **Step 2: 削除して全テストを再実行**

Run: `python3 -m unittest test_poller test_cmux_launcher test_transcript_probe 2>&1 | tail -3`
Expected: `OK`

- [ ] **Step 3: README.md の状態遷移図と説明を更新する**

冒頭段落「フェーズごとにheadless `claude` を起動して」→「フェーズごとに cmux ワークスペース上で対話モードの `claude` をフォアグラウンド起動して（ADR 0023）」。状態遷移図の `独立レビュー待ち` 節を以下に差し替え:

```
独立レビュー待ち
    │  メインクローンをorigin/masterへff-only同期（失敗なら待ちのまま据え置き）
    │  同時起動は2本まで（実行中＝生存 or limit待ち を数える）。cmux ping 失敗なら見送り＋1h毎通知
    │  ラベル付替え → 開始コメント → cmux workspace "pr-review <N>" で対話モード claude 起動
    ▼
独立レビュー:実行中 ──────────────────────────────┐
    │ findings.json検出(start以降のmtime・head一致) │ session limit: resetまで待ち→同ペインへ継続指示(LIMIT_CONTINUE_PROMPT)
    │ → ワークスペースclose → 裁定待ち             │ weekly limit: 失敗（人を呼ぶ）
    │                                              │ 生存かつ活動停止 IDLE_SECONDS(1200s): RESUME_PROMPT を1回送る → 2回目で失敗
    ▼                                              │ 死亡(pgrep無し)かつ findings 無し: retry<2 で新セッション・新ワークスペース
```
（apply 側も同様に「pr-apply <N>」「apply.session」「週次は失敗」を記す。）検知の節に「review.log/apply.log は廃止。観測点は transcript（`~/.claude/projects/*/<session>.jsonl` と `subagents/`）・findings/result・`cmux capture-pane`」を追記。

---

### Task 8: スキル側ドキュメントを新起動方式に合わせる（moorestech リポジトリ・要 worktree）

**Files:**
- Modify: `.agents/skills/pr-independent-review/SKILL.md:18-36`（「最重要: ターンを終えた瞬間にプロセスが死ぬ」節）
- Modify: `.agents/skills/pr-adjudicated-apply/SKILL.md`（`PR_REVIEW_UNATTENDED` / `claude -p` / print mode に言及する箇所。`grep -n "print mode\|claude -p\|PR_REVIEW_UNATTENDED" .agents/skills/pr-adjudicated-apply/SKILL.md` で特定）
- Modify: `docs/adr/0023-unattended-review-runs-in-cmux-foreground.md`（「状態: 採択」→ 実装 plan へのリンクを追記）

- [ ] **Step 1: worktree を切る**

Run: `moores-wt new feature/pr-review-cmux-foreground --no-editor` → 以降の編集は `~/moorestech-worktrees/feature/pr-review-cmux-foreground`（`moores-wt` が出力したパス）で行う。

- [ ] **Step 2: pr-independent-review/SKILL.md 18〜36行を差し替える**

```markdown
## 最重要: 無人起動でも「findings.json か abort.json で終える」

環境変数 `PR_REVIEW_UNATTENDED=1` が立っているとき、このスキルは poller から cmux ワークスペース上の
**対話モード** claude でフォアグラウンド起動されている（ADR 0023。2026-08-20 までは `claude -p` だった）。
対話モードではターンを終えてもプロセスは消えないが、**poller はあなたが動いているかを transcript の更新で見ている**。
session と subagents の transcript が 1200 秒更新されないと「自壊相当」と判定され、同じペインへ
RESUME 指示が1回送られ、それでも進まなければ失敗ラベルになる。

したがって無人起動時は:

- **待機は同一ターン内でブロッキングして行う**（subagent の完了待ちは Monitor 等で待ち切る）。
  「後で結果を確認します」とターンを閉じて待つことは、transcript が止まるため自壊と判定される
- **質問して停止することを禁止する**。判断が要る指摘はダイジェストの裁定カード（設計判断）へ落とす
- **終了地点は2つだけ** — Step 7.5 の `findings.json` が生成された直後か、下記「中止の申告」で
  `abort.json` を書いた直後
- **session limit に当たったら何もしなくてよい**。poller が reset 時刻まで待ち、同じペインへ
  「$RUNDIR/agents/*.md を点検し、オーケストレータのエージェントIDへ SendMessage で未完了分だけ続行」
  という継続指示を送る。その指示が来たら、完了済みの体は再派遣せず、保持しているIDへ SendMessage で続きを頼むこと
- 人がペインに割り込んで指示した場合は「止める」「続きを指示する」に限って従う

`PR_REVIEW_UNATTENDED` が無い（人が対話で起動した）場合は、質問して止まってよい。ただし
`findings.json` / `abort.json` のどちらかで終える規律は同じく守る。
```

- [ ] **Step 3: pr-adjudicated-apply/SKILL.md の該当箇所を同趣旨で更新する**

`print mode` / `claude -p` の記述を「cmux ワークスペース上の対話モード（ADR 0023）」に、「ターンを終えると死ぬ」を「poller は `apply-result.json` と プロセス生存で見ている。session limit は poller が reset 後に継続指示を送る」に置き換える。`apply-result.json` を書いて終える規律は不変。

- [ ] **Step 4: ADR 0023 に実装 plan へのリンクを足す**

`docs/adr/0023-unattended-review-runs-in-cmux-foreground.md` 末尾に `実装: docs/superpowers/plans/2026-08-20-pr-review-poller-cmux-foreground.md` を追記。

- [ ] **Step 5: コミット**

```bash
git add .agents/skills/pr-independent-review/SKILL.md .agents/skills/pr-adjudicated-apply/SKILL.md docs/adr/0023-unattended-review-runs-in-cmux-foreground.md
git commit -m "docs(skills): 無人レビュー/applyのcmux対話モード起動に合わせて終了規律とlimit時の振る舞いを更新（ADR 0023）"
```

---

### Task 9: 実機スモーク（cmux 上で本物の claude を1本・haiku）

**Files:**
- なし（検証のみ。state は一時ディレクトリ）

- [ ] **Step 1: 起動〜検知〜close を一周させる**

```bash
cd ~/hermes-agent/data/services/pr-review && PR_REVIEW_STATE_DIR=/tmp/pr-review-smoke python3 - <<'EOF'
import os, time, poller
poller.STATE_DIR = "/tmp/pr-review-smoke"; os.makedirs(poller.STATE_DIR, exist_ok=True)
poller.CLAUDE_MODEL = "haiku"
poller.DRYRUN = False
sid = poller.start_review_session(9999)
ok = poller.launch_claude(9999, "以下を実行せよ: Bash で `echo smoke > /tmp/pr-review-smoke/marker.txt` を実行し、'done' と返答せよ。", "review.pid", cwd="/tmp", session_id=sid)
print("launched", ok, "ws", poller.read_text(poller.workspace_path(9999, "review.pid")))
for i in range(30):
    time.sleep(5)
    alive = poller.session_alive(sid)
    print(i, "alive", alive, "activity", poller.probe.last_activity_epoch(sid) > 0, "marker", os.path.exists("/tmp/pr-review-smoke/marker.txt"))
    if os.path.exists("/tmp/pr-review-smoke/marker.txt"): break
print("last text:", poller.probe.last_assistant_text(sid)[:120])
print("screen:", poller.cmux.capture_tail(poller.read_text(poller.workspace_path(9999, "review.pid")), 5))
ws = poller.read_text(poller.workspace_path(9999, "review.pid"))
print("send continue:", poller.cmux.send_text(ws, "さらに 'second' と返答せよ"))
time.sleep(15)
print("last text after send:", poller.probe.last_assistant_text(sid)[:120])
poller.close_workspace_of(9999, "review.pid")
print("exists after close:", poller.cmux.workspace_exists(ws))
EOF
```
Expected: `launched True ws <UUID>`、数 tick 以内に `alive True` と `marker True`、`last text` に done、`send continue` True、`last text after send` に second、`exists after close: False`。この smoke で「対話モードでも transcript に assistant 発話が記録される」「同一ペインへの追送が効く」を実機確認する。

- [ ] **Step 2: 週次/セッション limit の文言検知は fixture で担保済み（Task 2・5）。実機では limit を意図的に起こさないため、transcript に limit 文言が載るかどうかは本番初回の limit 到達時に `state/pr-<N>/` と transcript を見て確認し、載らなければ capture-pane 経路（R6）が効いていることをログで確認する。** この確認結果を `bd note moorestech-vltk` に残す。

- [ ] **Step 3: 本番反映**

supervisor は `services.json` 不変のため再起動不要。次 tick（120 秒以内）から新経路。`tail -f ~/hermes-agent/data/services/always-on/logs/pr-review-poller.log` で `launched interactive claude in cmux workspace` が出ることを確認。初回の待ち PR が無ければ、`独立レビュー待ち` ラベルの付いたPRが出た時に観察する。

---

### Task 10: 全ブランチレビュー（必須・省略不可）

- [ ] **Step 1:** `moores-code-review` スキルで Task 8 のブランチ（`feature/pr-review-cmux-foreground`）をレビューし、指摘を適用する。poller 側（git 管理外）は `~/hermes-agent/data/services/pr-review/{poller.py,cmux_launcher.py,transcript_probe.py,test_*.py}` をレビュー対象パスとして同スキルに渡す（diff が取れないため「ファイル全文レビュー」で指示する）。
- [ ] **Step 2:** 指摘対応後に `python3 -m unittest test_poller test_cmux_launcher test_transcript_probe` が `OK` であることを再確認し、Task 8 ブランチを push して `pr-create` で PR を作る。

---

## 判断記録（ADR）

- ADR: `docs/adr/0023-unattended-review-runs-in-cmux-foreground.md`（cmux フォアグラウンド起動・同時2本・-p フォールバック無し）
- 裁定: `.decisions/2026-08-20-無人レビューは同時2本までcmuxフォアグラウンドで起動し限界死はreset後にSendMessageで継続する.md`
- 関連前例: `.decisions/2026-08-20-無人レビューの自壊対策はresumeとabort申告で入れる.md`（PR1193。本 plan の idle→RESUME 送信はその resume 予算 `MAX_REVIEW_RESUME=1` を継承）、`docs/superpowers/plans/2026-08-20-unattended-review-resume.md`
- planning 中の判断:
  - 生存検知を `pgrep -f "session-id <id>"` にした（出所: agent前提。対話モードは `sh -c` 経由でないため pid ファイルが取れず、session-id はコマンド行に必ず載る）
  - 自壊相当の検知を「session＋subagents transcript の最終更新から 1200 秒」にした（出所: agent前提。subagent が動いている間は親 transcript が止まるため subagents/ を含めないと誤検知する。1200 秒は orchestrator の wave 間隔＋余裕）
  - limit 文言は transcript と capture-pane の両方で見る（出所: agent前提。対話モードで limit 文言が transcript に残るかは未確認＝Global Constraints に明記。本番初回到達時に確認して bd note に残す）
  - 起動失敗時はラベルを動かさず、apply はスロットを返す（出所: agent前提。「実行中なのに何も走っていない」状態を作らない）
  - apply にも session-id を発行する（出所: agent前提。同じ検知経路に乗せるため。R12）
  - `cmux` は UUID で扱い、名前から引く（出所: 実機確認 2026-08-20。`workspace:NN` ref は close で詰まって変わる）
