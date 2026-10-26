#!/usr/bin/env python3
"""操作 A の並び順の計算。items と blocks は同じ規則で、それぞれの解放アクションから独立に並ぶ。

1. initialUnlocked を先頭（現状 sortPriority 順）
2. 研究の解放対象: 研究の depth → 同 depth は nodeGraph の y で行クラスタ（上の行が先・行内 x 昇順）
   → 各研究内は nodeGraph の x で列クラスタ（左の列が先・列内 y 昇順）。位置の無い対象は研究内の末尾。
   複数の研究が解放する対象は、研究順で最初の研究にだけ置く。
3. 孤立（どの研究も解放せず initialUnlocked でもない）を末尾（現状 sortPriority の相対順、同値は配列順）
sortPriority は 100 始まり 10 刻み。クラスタ閾値は 100（差がちょうど 100 は同じ行・列）。

Ordering for operation A. Items and blocks share the rule and are ordered independently from their own unlock actions:
initial unlocks first, then research targets by depth, nodeGraph rows (y) and in-research columns (x), then orphans.
A target unlocked by several researches is placed only under the first research. Priorities start at 100 in steps of 10.
"""
X_COLUMN_THRESHOLD = 100
Y_ROW_THRESHOLD = 100
SORT_START = 100
SORT_STEP = 10

INITIAL, RESEARCH, ORPHAN = "INIT", "RES", "ORPHAN"


class SortRole:
    """並べる対象の種類（ファイル・GUID キー・解放アクション）。 / Kind of sorted rows: file, GUID key and unlock action."""

    def __init__(self, label, file_name, guid_key, action_type, param_key):
        self.label = label
        self.file_name = file_name
        self.guid_key = guid_key
        self.action_type = action_type
        self.param_key = param_key


ITEM_ROLE = SortRole("items", "items.json", "itemGuid", "unlockItemRecipeView", "unlockItemGuids")
BLOCK_ROLE = SortRole("blocks", "blocks.json", "blockGuid", "unlockBlock", "unlockBlockGuids")
ROLES = (ITEM_ROLE, BLOCK_ROLE)


class Placement:
    """1 行の並び順と、その根拠（区分と解放した研究名）。 / One row's priority and its basis (kind and unlocking research)."""

    def __init__(self, guid, kind, research_name, priority):
        self.guid = guid
        self.kind = kind
        self.research_name = research_name
        self.priority = priority


def cluster_sorted(values_with_keys, threshold):
    # ソート済みの (key, item) を、隣との key の差が threshold 以下で続くクラスタに分ける
    # Split sorted (key, item) pairs into clusters while neighbouring keys differ by at most threshold
    clusters, current, last = [], [], None
    for key, item in values_with_keys:
        if last is not None and key - last > threshold:
            clusters.append(current)
            current = []
        current.append((key, item))
        last = key
    if current:
        clusters.append(current)
    return clusters


def node_positions(node_graph):
    # nodeGraph の座標を masterGuid で引く（note など masterGuid の無いノードは除く）
    # nodeGraph coordinates keyed by masterGuid (nodes without one, such as notes, are skipped)
    return {n["masterGuid"]: (n["position"]["x"], n["position"]["y"]) for n in node_graph["nodes"] if "masterGuid" in n}


def ordered_research(researches, positions):
    by_guid = {r["researchNodeGuid"]: r for r in researches}
    depth_memo = {}

    def depth(guid):
        if guid not in depth_memo:
            prevs = by_guid[guid]["prevResearchNodeGuids"]
            depth_memo[guid] = 0 if not prevs else max(depth(p) for p in prevs) + 1
        return depth_memo[guid]

    by_depth = {}
    for r in researches:
        # silent-ok: nodeGraph に無い研究は (0,0) とみなす / nodegraph_sync --check が未配置の研究を FAIL で出す
        by_depth.setdefault(depth(r["researchNodeGuid"]), []).append((positions.get(r["researchNodeGuid"], (0, 0)), r))
    result = []
    for d in sorted(by_depth):
        bucket = sorted(by_depth[d], key=lambda t: (t[0][1], t[0][0]))
        for row in cluster_sorted([(pos[1], (pos, r)) for pos, r in bucket], Y_ROW_THRESHOLD):
            result += [r for _, (_pos, r) in sorted(row, key=lambda t: t[1][0][0])]
    return result


def unlock_targets(research, role):
    return [guid for action in research.get("clearedActions", []) if action.get("gameActionType") == role.action_type
            for guid in action.get("gameActionParam", {}).get(role.param_key, [])]


def order_targets_in_research(research, role, positions):
    guids = unlock_targets(research, role)
    with_pos = sorted(((positions[g], g) for g in guids if g in positions), key=lambda t: (t[0][0], t[0][1]))
    result = []
    for column in cluster_sorted([(pos[0], (pos, g)) for pos, g in with_pos], X_COLUMN_THRESHOLD):
        result += [g for _, (_pos, g) in sorted(column, key=lambda t: t[1][0][1])]
    return result + [g for g in guids if g not in positions]


def plan_placements(rows, role, research_order, positions):
    """戻り値は (並び順の Placement 一覧, 行に無い解放対象の [(研究名, GUID)])。
    Returns (Placements in final order, [(research name, GUID)] of unlock targets missing from the rows)."""
    by_guid = {row[role.guid_key]: row for row in rows}
    order, placed, unknown = [], set(), []

    def place(guid, kind, research_name):
        order.append((guid, kind, research_name))
        placed.add(guid)

    for row in sorted((row for row in rows if row.get("initialUnlocked")), key=lambda row: row["sortPriority"]):
        place(row[role.guid_key], INITIAL, None)
    for research in research_order:
        for guid in order_targets_in_research(research, role, positions):
            if guid not in by_guid:
                unknown.append((research["researchNodeName"], guid))
            elif guid not in placed:
                place(guid, RESEARCH, research["researchNodeName"])
    for row in sorted((row for row in rows if row[role.guid_key] not in placed), key=lambda row: row["sortPriority"]):
        place(row[role.guid_key], ORPHAN, None)

    assert len(order) == len(rows) == len(placed), f"{role.label}: placed {len(order)} of {len(rows)} rows"
    return [Placement(g, k, r, SORT_START + i * SORT_STEP) for i, (g, k, r) in enumerate(order)], unknown


def apply_placements(rows, role, placements):
    # sortPriority を書き換え、配列もその順に並べ替えた新しい行の一覧を返す
    # Rewrite sortPriority and return the rows reordered by it
    by_guid = {row[role.guid_key]: row for row in rows}
    for placement in placements:
        by_guid[placement.guid]["sortPriority"] = placement.priority
    return [by_guid[placement.guid] for placement in placements]
