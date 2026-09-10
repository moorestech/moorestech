# =====================================================================
# ⚠ このscripts/配下を1行でも変更・追加したら、必ず回帰テストを実行すること:
#     python3 -m unittest discover -s .claude/skills/moores-code-review/tests
#   全緑になるまで変更は完成扱いにしない。新規スクリプトはSKILL.mdへの配線と
#   tests/test_skill_wiring.py への不変条件追加まで済ませて初めて完成（配線なき
#   検出器は未実装と同じ・2026-08-03ユーザー裁定）。このバナー自体も必須
#   （tests/test_skill_wiring.py が全スクリプトのバナー実在を機械検証する）。
# ⚠ Run the regression suite after ANY change under scripts/; wiring into
#   SKILL.md and a wiring-test invariant are part of "done" for new scripts.
# =====================================================================
"""moorestech 固有の決定論チェック。checks_static(汎用)が扱わない観点だけを持つ。

partial・try-catch・Func・200行・10ファイル・デフォルト引数・SerializeField 命名は
checks_static.py が包含するため、ここでは重複させない。ここが持つのは:
  confirmed  — master_default_fallback / packet_response_root / server_realtime_api / init_method_naming
  candidate  — schema_optional_true / event_tag_sync / server_elapsed_time
"""
from __future__ import annotations

import re
from pathlib import Path

from checks_static import _is_test_path
from cs_lex import strip_line
from patch_util import FileDiff

# マスタ欠損フォールバック: Default定数・??補完はスキーマ必須化+JSON更新で解決する
# Master-missing fallback: Default consts / ?? fills are forbidden — fix via required schema + JSON update
MASTER_DEFAULT_RE = re.compile(r"\?\?\s*\w*\.?Default[A-Z]|const\s+\w+\s+Default[A-Z]\w*\s*=")
OPTIONAL_TRUE_RE = re.compile(r"optional:\s*true")
EVENT_TAG_RE = re.compile(r'EventTag\s*=\s*"(va:event:[^"]+)"')

# サーバのゲームロジックの時間軸はGameUpdaterのティックだけ。実時間APIはフレームレート依存を持ち込む
# Server game logic measures time in GameUpdater ticks only; real-time APIs introduce frame-rate dependence
#
# confirmed 側は「経過時間の計測にしか使われない」APIだけ。正当用途が存在しないので裏取り不要
# The confirmed set holds APIs used only for elapsed-time measurement; they have no legitimate use here
REALTIME_API_RE = re.compile(
    r"\bTime\.(deltaTime|time|unscaledTime|realtimeSinceStartup|fixedDeltaTime)\b"
    r"|\bStopwatch\b"
    r"|\bEnvironment\.TickCount\b")

# DateTime は confirmed にできない: 「セーブに実世界の日時を記録する」正当用途（世界作成日時・
# 累計プレイ時間）と「ゲーム進行を経過時間でゲートする」違反が、同じ減算+TotalSecondsの形になるため
# 機械的に弁別できない。経過計測の痕跡がある場合だけ candidate に降ろし、verifier が用途を裁定する。
# DateTime cannot be confirmed: recording real-world timestamps in save data and gating game logic on
# elapsed time share the same subtraction/Total* shape, so a verifier must judge the purpose.
# 初期化メソッドの名前はInitialize固定 (AGENTS.md命名・構造の規約・PR1095人間裁定由来)。
# 厳密名 Init/Setup/Construct/Initialise のみ機械検出し、ApplyInitial等の意味判定は
# core-cs-region-internal reviewer が担う。overrideは基底の名前を継ぐしかないため除外。
# The initialization method must be named Initialize; only exact-name drift is detected here.
INIT_NAME_RE = re.compile(
    r"^\s*(?:public|internal|protected|private)\b"
    r"(?:\s+(?:static|async|virtual|sealed|new|partial))*"
    r"\s+[\w<>\[\],. ]+?\s+(?:Init|Setup|Construct|Initialise)\s*\(")
OVERRIDE_RE = re.compile(r"\boverride\b")

# VContainer注入メソッドの名前はConstruct固定 (2026-08-30裁定 W25/D9) なので[Inject]付きは対象外
# VContainer injection methods must stay named Construct (2026-08-30 ruling W25/D9), so [Inject]-attributed ones are exempt
INJECT_ATTRIBUTE_RE = re.compile(r"\bInject\b")

DATETIME_CLOCK_RE = re.compile(r"\bDateTime\.(Now|UtcNow)\b")
ELAPSED_MARKER_RE = re.compile(
    r"\bTimeSpan\b|\.Total(Seconds|Milliseconds|Minutes|Hours|Days)\b|Dictionary<[^>]*,\s*DateTime>")
SERVER_GAME_PREFIX = "moorestech_server/Assets/Scripts/Game."


def run_confirmed(files: list[FileDiff]) -> list[dict]:
    findings: list[dict] = []
    findings += _master_default_fallback(files)
    findings += _packet_response_root(files)
    findings += _server_realtime_api(files)
    findings += _init_method_naming(files)
    return findings


def _init_method_naming(files: list[FileDiff]) -> list[dict]:
    findings = []
    for f in files:
        if not f.path.endswith(".cs") or _is_test_path(f.path):
            continue
        # 削除行は新ファイルに残らないので、直前行の走査対象から外す
        # Removed lines are absent from the new file, so they are excluded from the preceding-line scan
        present = [entry for entry in f.lines if entry[0] != "-"]
        for index, (marker, lineno, text) in enumerate(present):
            if marker != "+" or lineno is None:
                continue
            code = strip_line(text)
            if not INIT_NAME_RE.search(code) or OVERRIDE_RE.search(code):
                continue
            if _is_inject_attributed(present, index):
                continue
            findings.append(_finding(
                "init-method-naming", f.path, lineno, text,
                "初期化メソッドの名前はInitialize固定 (AGENTS.md命名・構造の規約)。Init/Setup/Construct等の揺れは禁止。"
                "記述順はコンストラクタ→Initialize→以降の公開メソッド"))
    return findings


def _is_inject_attributed(present: list[tuple[str, int | None, str]], index: int) -> bool:
    """Walk the attribute lines directly above the method and report an [Inject] among them."""
    for _, _, text in reversed(present[:index]):
        code = strip_line(text).strip()
        if not code:
            continue
        if not code.startswith("["):
            return False
        if INJECT_ATTRIBUTE_RE.search(code):
            return True
    return False


def _server_realtime_api(files: list[FileDiff]) -> list[dict]:
    findings = []
    for f in files:
        if not (f.path.startswith(SERVER_GAME_PREFIX) and f.path.endswith(".cs")):
            continue
        if _is_test_path(f.path):
            continue
        for lineno, text in f.added():
            if REALTIME_API_RE.search(strip_line(text)):
                findings.append(_finding(
                    "server-realtime-api", f.path, lineno, text,
                    "サーバのゲームロジックの経過時間はGameUpdaterのティック加算のみ (AGENTS.md)。"
                    "Time.deltaTime/Stopwatch/Environment.TickCountは使わない。秒換算はGameUpdater.SecondsToTicks/TicksToSecondsを通す"))
    return findings


def _master_default_fallback(files: list[FileDiff]) -> list[dict]:
    findings = []
    for f in files:
        if not (f.path.endswith(".cs") and ("Core.Master" in f.path or "BlockTemplate" in f.path)):
            continue
        for lineno, text in f.added():
            if MASTER_DEFAULT_RE.search(strip_line(text)):
                findings.append(_finding(
                    "master-default-fallback", f.path, lineno, text,
                    "マスタ欠損フォールバック禁止: Default定数・??補完はスキーマ必須化+全JSON更新で解決する"))
    return findings


def _packet_response_root(files: list[FileDiff]) -> list[dict]:
    findings = []
    for f in files:
        if not (f.is_new and f.path.endswith(".cs")):
            continue
        if not str(Path(f.path).parent).endswith("Server.Protocol/PacketResponse"):
            continue
        content = "\n".join(t for _, t in f.added())
        if "IPacketResponse" not in content:
            findings.append(_finding(
                "packet-response-root", f.path, 1, f.path,
                "PacketResponse直下はIPacketResponse実装のみ。DTO/データクラスは別階層へ"))
    return findings


def schema_optional_true(files: list[FileDiff]) -> list[dict]:
    # optional:true は正当な例外（存在に意味があるフィールド）がありうるため candidate 扱い
    # optional:true has legitimate exceptions (presence-meaningful fields) so it stays a candidate
    findings = []
    for f in files:
        if not (f.path.startswith("VanillaSchema/") and f.path.endswith(".yml")):
            continue
        for lineno, text in f.added():
            if OPTIONAL_TRUE_RE.search(text):
                findings.append(_finding(
                    "schema-optional-true", f.path, lineno, text,
                    "optional:true新設候補: 原則禁止(必須化+default+全JSON更新が正)。『存在しないことに意味がある』フィールドのみ正当 — master-data-defenseレンズが裁定"))
    return findings


def event_tag_sync(files: list[FileDiff], patch_text: str, repo_root: Path) -> list[dict]:
    # 新規EventTagにクライアント購読が存在するか（diff内 or リポジトリ内）
    # For each new EventTag, verify a client-side subscription exists (in diff or repo)
    candidates = []
    client_root = repo_root / "moorestech_client" / "Assets" / "Scripts"
    for f in files:
        if "Server.Event" not in f.path:
            continue
        class_name = Path(f.path).stem
        for lineno, text in f.added():
            m = EVENT_TAG_RE.search(text)
            if not m:
                continue
            tag = m.group(1)
            subscribed = f"{class_name}.EventTag" in patch_text or tag in patch_text.replace(text, "")
            if not subscribed and client_root.is_dir():
                for cs in client_root.rglob("*.cs"):
                    src = cs.read_text(encoding="utf-8", errors="replace")
                    if f"{class_name}.EventTag" in src or tag in src:
                        subscribed = True
                        break
            if not subscribed:
                candidates.append(_finding(
                    "event-tag-sync", f.path, lineno, text,
                    f"新規イベント {tag} のクライアント購読(SubscribeEventResponse)が見つからない。3点セット(イベント+初期データ+購読)を確認"))
    return candidates


def server_elapsed_time(files: list[FileDiff]) -> list[dict]:
    """サーバGame配下で DateTime を経過時間計測に使っている疑いを候補として返す。

    同一ファイルの追加行に「経過計測の痕跡」（TimeSpan・Total*・DateTime辞書）がある DateTime.Now/UtcNow
    だけを候補にする。用途がセーブへの実世界時刻記録なのか、ゲーム進行のゲートなのかは verifier が裁定する。
    """
    candidates: list[dict] = []
    for f in files:
        if not (f.path.startswith(SERVER_GAME_PREFIX) and f.path.endswith(".cs")):
            continue
        if _is_test_path(f.path):
            continue
        added = [(no, strip_line(t)) for no, t in f.added()]
        if not any(ELAPSED_MARKER_RE.search(code) for _, code in added):
            continue
        for lineno, code in added:
            if DATETIME_CLOCK_RE.search(code):
                candidates.append(_finding(
                    "server-elapsed-time", f.path, lineno, code,
                    "サーバGame配下でDateTimeを経過時間計測に使っている疑い (AGENTS.md: 進行の経過時間はGameUpdaterのティック加算のみ)。"
                    "セーブへの実世界時刻記録（作成日時・累計プレイ時間）なら正当。用途をverifierが裁定する"))
    return candidates


def _finding(rule: str, path: str, line: int, evidence: str, message: str) -> dict:
    return {"rule": rule, "file": path, "line": line,
            "evidence": evidence.strip(), "message": message, "fix_class": "judgement"}
