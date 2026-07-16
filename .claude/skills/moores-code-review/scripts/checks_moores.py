"""moorestech 固有の決定論チェック。checks_static(汎用)が扱わない観点だけを持つ。

partial・try-catch・200行・10ファイル・デフォルト引数・SerializeField 命名は
checks_static.py が包含するため、ここでは重複させない。ここが持つのは:
  confirmed  — master_default_fallback / packet_response_root
  candidate  — schema_optional_true / event_tag_sync
"""
from __future__ import annotations

import re
from pathlib import Path

from cs_lex import strip_line
from patch_util import FileDiff

# マスタ欠損フォールバック: Default定数・??補完はスキーマ必須化+JSON更新で解決する
# Master-missing fallback: Default consts / ?? fills are forbidden — fix via required schema + JSON update
MASTER_DEFAULT_RE = re.compile(r"\?\?\s*\w*\.?Default[A-Z]|const\s+\w+\s+Default[A-Z]\w*\s*=")
OPTIONAL_TRUE_RE = re.compile(r"optional:\s*true")
EVENT_TAG_RE = re.compile(r'EventTag\s*=\s*"(va:event:[^"]+)"')


def run_confirmed(files: list[FileDiff]) -> list[dict]:
    findings: list[dict] = []
    findings += _master_default_fallback(files)
    findings += _packet_response_root(files)
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


def _finding(rule: str, path: str, line: int, evidence: str, message: str) -> dict:
    return {"rule": rule, "file": path, "line": line,
            "evidence": evidence.strip(), "message": message, "fix_class": "judgement"}
