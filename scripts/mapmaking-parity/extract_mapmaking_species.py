#!/usr/bin/env python3
"""
MapMakingのバイオームプリセットから樹種・岩のインベントリとtreePlacement設定を抽出しJSONへ出力する。
Extracts the tree/rock inventory and treePlacement settings from MapMaking biome presets into JSON.

出力 species-inventory.json は後続タスク（map.json生成・ラッパープレハブ生成・generation.json同期）の唯一の入力。
The emitted species-inventory.json is the sole input for the follow-up map.json, wrapper prefab, and generation.json tasks.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import schema_spec  # noqa: E402
import species_catalog  # noqa: E402
from object_config_extractor import convert_object_config, iter_object_prefab_references, object_config_schema  # noqa: E402
from prototype_converter import PrototypeConverter  # noqa: E402
from unity_asset_yaml import build_prefab_guid_index, load_unity_asset, reference_guid  # noqa: E402

# 現行スキーマ（prototypes[].prefabs[] のguid配列）を持つプリセット
# Presets on the current schema (a guid array at prototypes[].prefabs[])
CURRENT_SCHEMA_BIOMES = ("Forest", "Grassland", "Savanna", "Mesa")

# 旧スキーマ（prototypes[].prefab 単数）のプリセット。樹種リストのみ抽出する
# Presets on the legacy schema (a single prototypes[].prefab); only the species list is extracted
LEGACY_SCHEMA_BIOMES = ("Jungle", "Woods")

# 全プロトタイプがdisabledで配置設定を持たないプリセット。樹種の登録だけを行い biomes には出さない
# Presets whose prototypes are all disabled: their species are registered but no biome entry is emitted
SPECIES_ONLY_BIOMES = ("Desert",)

# objectConfigを抽出する全プリセット（treePlacementと違い全8バイオームが同一スキーマ）
# Every preset whose objectConfig is extracted (unlike treePlacement, all 8 biomes share one schema)
OBJECT_CONFIG_BIOMES = CURRENT_SCHEMA_BIOMES + LEGACY_SCHEMA_BIOMES + SPECIES_ONLY_BIOMES + ("Alpine",)

# TreePrototypeEntry改修後に再保存されておらず、新フィールドが未記録・旧フィールドが残るプリセット
# Presets not re-saved since the TreePrototypeEntry rework: new fields unwritten, removed fields still present
STALE_SERIALIZED_BIOMES = frozenset({"Mesa"})

# 現行のTreePrototypeEntryから削除済みでUnityが読み捨てるフィールド（実データは全て無効値であることを確認済み）
# Fields removed from the current TreePrototypeEntry that Unity discards; all carry inert values here
REMOVED_UNITY_FIELDS = frozenset({
    "poolId", "heightWeightCurve", "heightFilter",
    "overrideClustering", "overrideHeightMod", "overrideSurroundLayer",
    "overrideBoundaryScale", "overrideOldGrowth",
})

# 未再保存プリセットに欠けており、スキーマ既定値で補うことを許すフィールド（実測と一致することを検算する）
# Fields absent from the stale preset that may be filled from the schema default, cross-checked each run
STALE_MISSING_FIELDS = frozenset({
    "densityConfig", "understoryConfig", "rockProximityConfig",
    "borderMargin", "sharedGridMinDistance",
})

REPO_ROOT = Path(__file__).resolve().parents[2]
BIOME_PRESET_DIR = REPO_ROOT / "TmpUnityPjt/MapMaking/Assets/MapGenerator/Presets/Biomes"
CLIENT_ASSETS_ROOT = REPO_ROOT / "moorestech_client/Assets"
PRIVATE_ASSETS_ROOT = CLIENT_ASSETS_ROOT / "PersonalAssets/moorestech-client-private"
SCHEMA_DIR = REPO_ROOT / "VanillaSchema/mapGenerate"
OUTPUT_PATH = Path(__file__).resolve().parent / "species-inventory.json"


def main() -> None:
    prefab_path_by_guid = build_prefab_guid_index(PRIVATE_ASSETS_ROOT, CLIENT_ASSETS_ROOT)
    prototype_schema = _prototype_schema()

    # 全プリセットを先に読み、樹種を確定させてからプロトタイプ変換でguidを引く
    # Read every preset first so species are settled before prototype conversion resolves guids
    unity_biomes = {name: _load_tree_placement(name)
                    for name in CURRENT_SCHEMA_BIOMES + LEGACY_SCHEMA_BIOMES + SPECIES_ONLY_BIOMES}
    unity_object_configs = {name: _load_object_config(name) for name in OBJECT_CONFIG_BIOMES}
    species_by_guid = _collect_species(unity_biomes, unity_object_configs, prefab_path_by_guid)
    map_object_guid_by_prefab_guid = {guid: species.map_object_guid
                                      for guid, species in species_by_guid.items()}

    _reject_removed_fields_still_in_schema(prototype_schema)

    biomes: dict = {}
    for name in CURRENT_SCHEMA_BIOMES:
        stale = name in STALE_SERIALIZED_BIOMES
        # treePlacementのsurroundLayerは未設定＝重み0で不使用なので空文字のまま
        # treePlacement's surroundLayer is unset and unused at weight 0, so it stays ""
        converter = PrototypeConverter(map_object_guid_by_prefab_guid, stale,
                                       REMOVED_UNITY_FIELDS, STALE_MISSING_FIELDS, "")
        prototypes = [
            converter.convert(prototype_schema, prototype,
                              f"{name}.treePlacement.prototypes[{index}]")
            for index, prototype in enumerate(unity_biomes[name]["prototypes"])
        ]
        if stale:
            converter.reject_missing_field_mismatch(f"{name}.treePlacement")
        # disabled のプロトタイプは配置設定から除外する（樹種一覧には残す）
        # Disabled prototypes are dropped from the placement config but kept in the species list
        biomes[name.lower()] = {"prototypes": [p for p in prototypes if not p["disabled"]]}

    for name in LEGACY_SCHEMA_BIOMES:
        # 現行スキーマ側(:90)と同じくdisabledは配置対象から除外する（樹種一覧には既に_collect_speciesで登録済み）
        # Same as the current-schema side (:90), disabled prototypes are dropped from placement (already registered in the species list by _collect_species)
        biomes[name.lower()] = {"speciesFill": [
            map_object_guid_by_prefab_guid[reference_guid(prototype["prefab"], f"{name}[{index}]")]
            for index, prototype in enumerate(unity_biomes[name]["prototypes"])
            if not _legacy_disabled(prototype, f"{name}[{index}]")
        ]}

    _reject_placeable_species_only_biomes(unity_biomes)

    # objectConfigは配置の有無に関わらず全8バイオームぶん写す（Forest/Grasslandは空prefabsの死にエントリも原本どおり）
    # objectConfig is transcribed for all 8 biomes regardless of placement (Forest/Grassland keep their empty-prefab dead entries as-is)
    schema = object_config_schema(SCHEMA_DIR)
    for name in OBJECT_CONFIG_BIOMES:
        biomes.setdefault(name.lower(), {})["objectConfig"] = convert_object_config(
            schema, unity_object_configs[name], name, map_object_guid_by_prefab_guid)

    species = sorted(species_by_guid.values(), key=lambda entry: entry.key)
    document = {"species": [entry.to_json() for entry in species], "biomes": biomes}
    OUTPUT_PATH.write_text(json.dumps(document, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    _report(species, biomes)


def _prototype_schema() -> schema_spec.SchemaNode:
    config_schema = schema_spec.load_schema(SCHEMA_DIR, "treePlacementConfig")
    prototypes = dict(config_schema.properties)["prototypes"]
    return prototypes.item


def _reject_removed_fields_still_in_schema(prototype_schema) -> None:
    """削除済み扱いのフィールドがスキーマに現存していないことを検算する。
    Verifies that fields treated as removed are genuinely absent from the schema."""
    resurrected = sorted(REMOVED_UNITY_FIELDS & {key for key, _ in prototype_schema.properties})
    if resurrected:
        raise ValueError(f"削除済みとみなしたフィールドがスキーマに存在する: {resurrected}")


def _reject_placeable_species_only_biomes(unity_biomes: dict) -> None:
    """樹種のみ抽出するプリセットに有効プロトタイプが現れていないことを検算する。
    Verifies that species-only presets still hold no enabled prototype to place."""
    for name in SPECIES_ONLY_BIOMES:
        enabled = [index for index, prototype in enumerate(unity_biomes[name]["prototypes"])
                   if not prototype["disabled"]]
        if enabled:
            raise ValueError(f"{name}: 樹種のみ抽出の前提に反し有効プロトタイプがある: {enabled}")


def _legacy_disabled(prototype: dict, label: str) -> bool:
    """旧スキーマprototypeのdisabledを読む。キー欠落は生データの想定外なのでfail-fastする。
    Reads a legacy-schema prototype's disabled flag; a missing key is unexpected raw data, so it fails fast."""
    if "disabled" not in prototype:
        raise KeyError(f"{label}: disabledが無い")
    return bool(prototype["disabled"])


def _load_object_config(biome_name: str) -> dict:
    asset = load_unity_asset(BIOME_PRESET_DIR / f"{biome_name}.asset")
    if "objectConfig" not in asset:
        raise KeyError(f"{biome_name}.asset: objectConfigが無い")
    return asset["objectConfig"]


def _load_tree_placement(biome_name: str) -> dict:
    asset = load_unity_asset(BIOME_PRESET_DIR / f"{biome_name}.asset")
    if "treePlacement" not in asset:
        raise KeyError(f"{biome_name}.asset: treePlacementが無い")
    return asset["treePlacement"]


def _collect_species(unity_biomes: dict, unity_object_configs: dict, prefab_path_by_guid: dict) -> dict:
    """全プリセットのプロトタイプ参照とobjectConfig参照から種を洗い出す（disabled分・密度0分も登録する）。
    Enumerates species from every preset's prototype and objectConfig references, disabled and zero-density ones included."""
    species_by_guid: dict = {}

    def register(guid: str, location: str):
        if guid in species_by_guid:
            return species_by_guid[guid]
        if guid not in prefab_path_by_guid:
            raise KeyError(f"{location}: guid {guid} のプレハブが非公開アセット内に見つからない")
        species_by_guid[guid] = species_catalog.build_species(guid, prefab_path_by_guid[guid])
        return species_by_guid[guid]

    for biome_name, tree_placement in unity_biomes.items():
        for index, prototype in enumerate(tree_placement["prototypes"]):
            location = f"{biome_name}.treePlacement.prototypes[{index}]"
            references = prototype["prefabs"] if "prefabs" in prototype else [prototype["prefab"]]
            for reference in references:
                register(reference_guid(reference, location), location)

    # objectConfig経由の参照は裸地化判定（bareGround）の材料になるので、参照元を種に記録する
    # References via objectConfig feed the bare-ground decision, so the origin is recorded on the species
    for biome_name, object_config in unity_object_configs.items():
        for guid, location in iter_object_prefab_references(object_config, biome_name):
            register(guid, location).referenced_by_object_config = True

    # keyはmapObjectGuidの採番元。別プレハブが同keyになるとguidが衝突するので弾く
    # key seeds the mapObjectGuid, so two prefabs sharing a key would collide and must be rejected
    keys_seen: dict = {}
    for species in species_by_guid.values():
        if species.key in keys_seen:
            raise ValueError(f"key衝突 {species.key}: {keys_seen[species.key]} と {species.prefab_path}")
        keys_seen[species.key] = species.prefab_path
    return species_by_guid


def _report(species: list, biomes: dict) -> None:
    kinds: dict = {}
    for entry in species:
        kinds[entry.kind] = kinds.get(entry.kind, 0) + 1
    print(f"species {len(species)} ({', '.join(f'{k} {v}' for k, v in sorted(kinds.items()))})")
    for name, biome in biomes.items():
        parts = []
        if "prototypes" in biome:
            parts.append(f"prototypes {len(biome['prototypes'])}")
        if "speciesFill" in biome:
            parts.append(f"speciesFill {len(biome['speciesFill'])}")
        object_config = biome["objectConfig"]
        parts.append(f"objectConfig entries {len(object_config['entries'])} clusters {len(object_config['clusterEntries'])}")
        print(f"{name}: " + ", ".join(parts))
    print(f"wrote {OUTPUT_PATH}")


if __name__ == "__main__":
    main()
