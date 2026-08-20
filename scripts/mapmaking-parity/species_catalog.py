"""
プレハブパスから樹種・岩のインベントリ項目（key・kind・アドレス・決定論guid）を組み立てる。
Builds tree/rock inventory entries (key, kind, address, deterministic guid) from prefab paths.
"""

from __future__ import annotations

import uuid

# 決定論採番の名前空間。再実行でmapObjectGuidを不変に保つため固定値とする
# Namespace for deterministic ids; fixed so mapObjectGuid stays stable across reruns
MAP_OBJECT_NAMESPACE = uuid.UUID("3f2c9a6e-8b41-4d5a-9c73-1e0a6b2d4f88")

PRIVATE_ASSETS_PREFIX = "Assets/PersonalAssets/moorestech-client-private/"
BK_SEGMENT = "BK"
ADDRESS_ROOT = "Vanilla/Environment"
WRAPPER_ROOT = "Assets/AddressableResources/Environment"

# kindごとのアドレス分類。pebbleは岩側に置く。plant/propはobjectConfig専用（Mesaの低木・小物）
# Address category per kind; pebbles live on the rock side, plant/prop exist only for objectConfig (Mesa shrubs and props)
ADDRESS_CATEGORY_BY_KIND = {"tree": "Tree", "rock": "Rock", "pebble": "Rock", "plant": "Plant", "prop": "Prop"}

# 移植元TerrainGenerator.ApplyObjectSurroundTextureは、objectConfig配置のうち名前にこれを含む岩だけ裸地化する
# The source TerrainGenerator.ApplyObjectSurroundTexture repaints bare ground only under objectConfig placements whose name contains one of these
BARE_GROUND_NAME_MARKERS = ("Boulder", "Cliff")


class Species:
    def __init__(self, prefab_guid: str, prefab_path: str):
        self.prefab_guid = prefab_guid
        self.prefab_path = prefab_path
        self.pack_short_name = _pack_short_name(prefab_path)
        self.name = prefab_path.rsplit("/", 1)[1][: -len(".prefab")]
        self.key = f"{self.pack_short_name}/{self.name}"
        self.kind = _kind_of(prefab_path, self.name)
        category = ADDRESS_CATEGORY_BY_KIND[self.kind]
        self.address = f"{ADDRESS_ROOT}/{category}/{self.key}"
        self.wrapper_path = f"{WRAPPER_ROOT}/{category}/{self.key}.prefab"
        self.map_object_guid = str(
            uuid.uuid5(MAP_OBJECT_NAMESPACE, f"moorestech.mapobject.{self.key}"))
        # objectConfigから参照された種だけがtrueになりうる。treePlacement経由の岩は移植元で裸地化されない
        # Only species referenced from objectConfig can become true; rocks placed via treePlacement are never repainted in the source
        self.referenced_by_object_config = False

    @property
    def bare_ground(self) -> bool:
        return self.referenced_by_object_config and any(
            marker in self.name for marker in BARE_GROUND_NAME_MARKERS)

    def to_json(self) -> dict:
        return {
            "key": self.key,
            "prefabGuid": self.prefab_guid,
            "prefabPath": self.prefab_path,
            "kind": self.kind,
            "address": self.address,
            "wrapperPath": self.wrapper_path,
            "mapObjectGuid": self.map_object_guid,
            "mapObjectName": self.name,
            "bareGround": self.bare_ground,
        }


def build_species(prefab_guid: str, prefab_path: str) -> Species:
    return Species(prefab_guid, prefab_path)


def _kind_of(prefab_path: str, name: str) -> str:
    """小石は岩フォルダに同居するため、Pebble判定を岩判定より先に行う。
    Pebbles share the rock folder, so the Pebble check must precede the rock check."""
    if name.startswith("Pebble"):
        return "pebble"
    if "/Rocks/" in prefab_path or "/Rubble/" in prefab_path:
        return "rock"
    if "/Plants/" in prefab_path:
        return "plant"
    if "/Props/" in prefab_path:
        return "prop"
    return "tree"


def _pack_short_name(prefab_path: str) -> str:
    """BK配下のパック名を短縮する（PureNature_Xxx→Xxx、PureNature→Base）。
    Shortens the pack directory under BK (PureNature_Xxx to Xxx, PureNature to Base)."""
    if not prefab_path.startswith(PRIVATE_ASSETS_PREFIX):
        raise ValueError(f"非公開アセット配下でないプレハブ: {prefab_path}")

    segments = prefab_path[len(PRIVATE_ASSETS_PREFIX):].split("/")
    if segments[0] != BK_SEGMENT:
        raise ValueError(f"BK配下でないプレハブ: {prefab_path}")

    pack = segments[1]
    if pack == "PureNature":
        return "Base"
    if pack.startswith("PureNature_"):
        return pack[len("PureNature_"):]
    raise ValueError(f"短縮名の規則に当てはまらないパック {pack}: {prefab_path}")
