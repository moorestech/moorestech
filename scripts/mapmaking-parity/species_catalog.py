"""
プレハブパスから樹種・岩のインベントリ項目（key・kind・アドレス・決定論guid）を組み立てる。
Builds tree/rock inventory entries (key, kind, address, deterministic guid) from prefab paths.
"""

from __future__ import annotations

import json
import pathlib
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

# 原木を落とす樹種の宣言表。プレハブ名からの推測は誤分類が静かに通るため持たない（ユーザー裁定 2026-08-23）
# Declared table of log-dropping species; names are never guessed, since a misread would pass silently (user adjudication 2026-08-23)
TIMBER_SPECIES_PATH = pathlib.Path(__file__).with_name("timber-species.json")

# 装飾物（狙えず削れない種）の宣言表。装飾かどうかは上流のプリセットに情報源が無く人手宣言だけが権威
# Declared table of decoration species; nothing upstream records it, so the human declaration is the only authority
DECORATION_SPECIES_PATH = pathlib.Path(__file__).with_name("decoration-species.json")

# 遠景可視軸（ランドマークか距離カリングか）の宣言表。こちらも上流に情報源が無く人手宣言だけが権威
# Declared table of the distance-visibility axis; nothing upstream records it either, so the human declaration is the only authority
LANDMARK_SPECIES_PATH = pathlib.Path(__file__).with_name("landmark-species.json")


def _load_timber_declaration() -> tuple[frozenset[str], frozenset[str]]:
    document = json.loads(TIMBER_SPECIES_PATH.read_text(encoding="utf-8"))
    timber = frozenset(document["timber"])
    non_timber = frozenset(document["nonTimber"])

    overlap = timber & non_timber
    if overlap:
        raise ValueError(f"timberとnonTimberの両方に宣言された樹種: {sorted(overlap)}")

    return timber, non_timber


TIMBER_KEYS, NON_TIMBER_KEYS = _load_timber_declaration()


def _load_decoration_declaration() -> tuple[frozenset[str], frozenset[str]]:
    document = json.loads(DECORATION_SPECIES_PATH.read_text(encoding="utf-8"))
    decoration = frozenset(document["decoration"])
    interactive = frozenset(document["interactive"])

    overlap = decoration & interactive
    if overlap:
        raise ValueError(f"decorationとinteractiveの両方に宣言された種: {sorted(overlap)}")

    return decoration, interactive


DECORATION_KEYS, INTERACTIVE_KEYS = _load_decoration_declaration()


def _load_landmark_declaration() -> tuple[frozenset[str], frozenset[str]]:
    document = json.loads(LANDMARK_SPECIES_PATH.read_text(encoding="utf-8"))
    landmark = frozenset(document["landmark"])
    cullable = frozenset(document["cullable"])

    overlap = landmark & cullable
    if overlap:
        raise ValueError(f"landmarkとcullableの両方に宣言された種: {sorted(overlap)}")

    return landmark, cullable


LANDMARK_KEYS, CULLABLE_KEYS = _load_landmark_declaration()

# ドロップ軸の値。earn_itemsはこの値だけを見て落とし物を決める
# Drop-axis values; earn_items decides drops from this value alone
DROP_CLASS_LOG = "log"
DROP_CLASS_STONE = "stone"
DROP_CLASS_NONE = "none"


def declared_drop_class(key: str) -> str | None:
    """宣言表が定めるドロップ軸を返す（未宣言はNone）。生成器と検証で同じ規則を共有する。
    Returns the drop class the declaration assigns, or None when undeclared; shared by the generator and its validation."""
    if key in TIMBER_KEYS:
        return DROP_CLASS_LOG
    if key in NON_TIMBER_KEYS:
        return DROP_CLASS_NONE
    return None


# 相互作用軸の値。miningTypeはこの値と kind だけを見て決まる
# Interaction-axis values; miningType is decided from this value and the kind alone
INTERACTION_CLASS_DECORATION = "decoration"
INTERACTION_CLASS_INTERACTIVE = "interactive"

# 遠景可視軸の値。map.ymlのdistanceVisibilityTypeへそのまま載る
# Distance-visibility values; written straight into distanceVisibilityType in map.yml
DISTANCE_VISIBILITY_LANDMARK = "landmark"
DISTANCE_VISIBILITY_CULLABLE = "cullable"


def declared_interaction_class(key: str) -> str | None:
    """宣言表が定める相互作用軸を返す（未宣言はNone）。生成器と検証で同じ規則を共有する。
    Returns the interaction class the declaration assigns, or None when undeclared; shared by the generator and its validation."""
    if key in DECORATION_KEYS:
        return INTERACTION_CLASS_DECORATION
    if key in INTERACTIVE_KEYS:
        return INTERACTION_CLASS_INTERACTIVE
    return None


def declared_distance_visibility(key: str) -> str | None:
    """宣言表が定める遠景可視軸を返す（未宣言はNone）。生成器と検証で同じ規則を共有する。
    Returns the distance visibility the declaration assigns, or None when undeclared; shared by the generator and its validation."""
    if key in LANDMARK_KEYS:
        return DISTANCE_VISIBILITY_LANDMARK
    if key in CULLABLE_KEYS:
        return DISTANCE_VISIBILITY_CULLABLE
    return None


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

    # 何を落とす種か。装飾物は削れないので落とし物を持てず、相互作用軸がドロップ軸を従属させる
    # What the species drops; a decoration can never be mined, so the interaction axis subordinates the drop axis
    @property
    def drop_class(self) -> str:
        if self.interaction_class == INTERACTION_CLASS_DECORATION:
            return DROP_CLASS_NONE

        declared = declared_drop_class(self.key)
        if declared is not None:
            if self.kind != "tree":
                raise ValueError(
                    f"{TIMBER_SPECIES_PATH.name} は kind=tree の宣言表だが {self.key} の kind は {self.kind}")
            return declared

        if self.kind == "tree":
            raise ValueError(
                f"timber未宣言の樹種 {self.key}: {TIMBER_SPECIES_PATH.name} の timber / nonTimber のどちらかへ足すこと")
        if self.kind == "plant":
            return DROP_CLASS_NONE
        return DROP_CLASS_STONE

    # 狙える種か装飾物か。上流に情報源が無いため宣言表だけが決め、未宣言は静かに採掘対象にせず止める
    # Whether the species can be aimed at or is decoration; only the declaration decides, and an undeclared species stops generation instead of silently becoming minable
    @property
    def interaction_class(self) -> str:
        declared = declared_interaction_class(self.key)
        if declared is None:
            raise ValueError(
                f"相互作用軸が未宣言の種 {self.key}: {DECORATION_SPECIES_PATH.name} の decoration / interactive のどちらかへ足すこと")
        return declared

    # 遠景で消えるか残るか。上流に情報源が無いため宣言表だけが決め、未宣言は静かにcullableにせず止める
    # Whether it survives distance culling; only the declaration decides, and an undeclared species stops generation instead of silently becoming cullable
    @property
    def distance_visibility_type(self) -> str:
        declared = declared_distance_visibility(self.key)
        if declared is None:
            raise ValueError(
                f"遠景可視軸が未宣言の種 {self.key}: {LANDMARK_SPECIES_PATH.name} の landmark / cullable のどちらかへ足すこと")
        return declared

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
            "dropClass": self.drop_class,
            "interactionClass": self.interaction_class,
            "distanceVisibilityType": self.distance_visibility_type,
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
