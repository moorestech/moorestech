"""チャレンジ・研究の GUID を表示名と並び順へ引く（マスタデータ JSON を読むだけ）。
マスタが無い・読めないときは空の表で返し、表示側は GUID のまま出す（集計は止めない）。

Maps challenge and research GUIDs to display names and order by reading the master JSON.
When the master is missing or unreadable an empty table is returned and the view shows raw GUIDs.
"""
from __future__ import annotations

from pathlib import Path

import digest_schema as schema
from display_fields import warn

CHALLENGE_SCHEMA = {"data": ([{
    "categoryName": (schema.STR, ""), "displayOrder": (schema.NUMBER, 0),
    "challenges": ([{"challengeGuid": (schema.STR, ""), "title": (schema.STR, "")}], None),
}], None)}
RESEARCH_SCHEMA = {"data": ([{"researchNodeGuid": (schema.STR, ""), "researchNodeName": (schema.STR, "")}], None)}


def load_master_names(master_dir: Path) -> dict:
    """チャレンジはカテゴリの displayOrder→定義順で通し番号を振る（到達ファネルの横軸）
    Challenges are numbered by category displayOrder then definition order (the reach funnel's axis)"""
    return {"challenges": load_challenges(master_dir / "challenges.json"),
            "research": load_research(master_dir / "research.json")}


def load_challenges(path: Path) -> list[dict]:
    data, reason = schema.read_conformed(path, CHALLENGE_SCHEMA)
    if data is None:
        warn(f"チャレンジ名を引けない（GUIDのまま表示）: {reason}", path)
        return []
    categories = sorted(data["data"], key=lambda category: category["displayOrder"])
    return [{"guid": challenge["challengeGuid"], "title": challenge["title"], "category": category["categoryName"]}
            for category in categories for challenge in category["challenges"]]


def load_research(path: Path) -> dict[str, str]:
    data, reason = schema.read_conformed(path, RESEARCH_SCHEMA)
    if data is None:
        warn(f"研究名を引けない（GUIDのまま表示）: {reason}", path)
        return {}
    return {node["researchNodeGuid"]: node["researchNodeName"] for node in data["data"]}
