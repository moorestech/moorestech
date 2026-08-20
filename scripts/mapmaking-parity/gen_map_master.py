#!/usr/bin/env python3
"""species-inventory.jsonからmaster map.jsonへmapObjectsを一括追記する（冪等）。

樹種・岩のmapObjectをVanillaSchema/map.ymlのmapObjectsスキーマに厳密一致する形で生成し、
同じmapObjectGuidが既にあれば置換、無ければ末尾へ追加する。採掘設定は既存「木」「小石」の複製。

Generates map objects for tree/rock species that match the mapObjects schema in VanillaSchema/map.yml exactly,
replacing entries with the same mapObjectGuid and appending new ones. Mining settings are copied from the existing tree/pebble.
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
INVENTORY = ROOT / "scripts/mapmaking-parity/species-inventory.json"
MASTER = ROOT.parent / "moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json"

WOOD_ITEM = "aafce615-6c30-48c4-a29e-3c5b3266748f"   # 原木（既存「木」のドロップ） / Log
STONE_ITEM = "582040ec-093b-4c8e-8fe3-f4ec030cf1ca"  # 石（既存「小石」のドロップ） / Stone
MINING_TOOLS = [
    {"toolItemGuid": "4c5fefbd-60a4-42ea-b70a-38a83b96e25e", "damage": 25, "attackSpeed": 1},
    {"toolItemGuid": "76174235-48fb-4944-bca7-ad268385d68c", "damage": 10, "attackSpeed": 2},
]

# スキーマ上のmapObject要素のキー集合（この集合と完全一致しない生成物はfail-fastで弾く）
# The exact key set of a mapObject element in the schema; anything else fails fast
SCHEMA_KEYS = {
    "mapObjectGuid", "mapObjectName", "addressablePath", "hp", "earnItemHpInterval",
    "soundEffectType", "terrainSurroundEffectType", "earnItems", "miningType", "miningParam",
}
# kindごとの効果音。plantは低木・草なので木、propは小物なので石の音を鳴らす。未知のkindはfail-fastさせる
# Sound per kind; plants are shrubs and grasses so they sound like trees, props sound like stone. Unknown kinds fail fast
KIND_SOUND_EFFECTS = {"tree": "tree", "rock": "stone", "pebble": "stone", "plant": "tree", "prop": "stone"}

# 木だけが木用距離場＋根元パッチ。それ以外はオブジェクト用距離場で、裸地を塗るのは移植元が裸地化する種（bareGround）に限る
# Only trees feed the tree field with root patches; everything else feeds the object field, and bare ground is painted only for species the source repaints (bareGround)
def terrain_surround_effect_type(species: dict) -> str:
    if species["kind"] == "tree":
        return "treeRootPatch"
    return "rockBareGround" if species["bareGround"] else "rockNoBareGround"


def build_entry(species: dict) -> dict:
    kind = species["kind"]
    if kind not in KIND_SOUND_EFFECTS:
        raise ValueError(f"unknown kind: {kind} ({species['key']})")
    sound_effect_type = KIND_SOUND_EFFECTS[kind]

    # 小石はPickUp（HP1・道具不要）、樹木・低木と岩・小物はMining（既存「木」の設定を複製）。ドロップは木・低木が原木、他は石
    # Pebbles are picked up bare-handed; trees/plants and rocks/props are mined with the existing tree's settings. Trees/plants drop logs, the rest stone
    is_pebble = kind == "pebble"
    earn_item = WOOD_ITEM if kind in ("tree", "plant") else STONE_ITEM
    entry = {
        "mapObjectGuid": species["mapObjectGuid"],
        "mapObjectName": species["mapObjectName"],
        "addressablePath": species["address"],
        "hp": 1 if is_pebble else 100,
        "earnItemHpInterval": 1 if is_pebble else 10,
        "soundEffectType": sound_effect_type,
        "terrainSurroundEffectType": terrain_surround_effect_type(species),
        "earnItems": [{"itemGuid": earn_item, "minCount": 1, "maxCount": 1 if is_pebble else 4}],
        "miningType": "PickUp" if is_pebble else "Mining",
        "miningParam": {} if is_pebble else {"miningTools": [dict(t) for t in MINING_TOOLS]},
    }
    if set(entry) != SCHEMA_KEYS:
        raise ValueError(f"key set mismatch: {sorted(set(entry) ^ SCHEMA_KEYS)}")
    return entry


def main() -> None:
    inventory = json.loads(INVENTORY.read_text(encoding="utf-8"))
    master = json.loads(MASTER.read_text(encoding="utf-8"))
    by_guid = {o["mapObjectGuid"]: i for i, o in enumerate(master["mapObjects"])}

    added = replaced = 0
    for species in inventory["species"]:
        entry = build_entry(species)
        index = by_guid.get(entry["mapObjectGuid"])
        if index is None:
            by_guid[entry["mapObjectGuid"]] = len(master["mapObjects"])
            master["mapObjects"].append(entry)
            added += 1
        else:
            master["mapObjects"][index] = entry
            replaced += 1

    # 追記後のguid重複はマスタロードを壊すので書き出す前に検出する
    # A duplicated guid would break the master load, so detect it before writing
    guids = [o["mapObjectGuid"] for o in master["mapObjects"]]
    if len(guids) != len(set(guids)):
        raise ValueError("duplicated mapObjectGuid after merge")

    MASTER.write_text(json.dumps(master, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"added={added} replaced={replaced} total={len(master['mapObjects'])}")


if __name__ == "__main__":
    main()
