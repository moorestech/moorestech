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
from prototype_converter import PrototypeConverter  # noqa: E402
from unity_asset_yaml import build_prefab_guid_index, load_unity_asset, reference_guid  # noqa: E402

# 現行スキーマ（prototypes[].prefabs[] のguid配列）を持つプリセット
# Presets on the current schema (a guid array at prototypes[].prefabs[])
CURRENT_SCHEMA_BIOMES = ("Forest", "Grassland", "Savanna", "Mesa")

# 旧スキーマ（prototypes[].prefab 単数）のプリセット。樹種リストのみ抽出する
# Presets on the legacy schema (a single prototypes[].prefab); only the species list is extracted
LEGACY_SCHEMA_BIOMES = ("Jungle", "Woods")

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
                    for name in CURRENT_SCHEMA_BIOMES + LEGACY_SCHEMA_BIOMES}
    species_by_guid = _collect_species(unity_biomes, prefab_path_by_guid)
    map_object_guid_by_prefab_guid = {guid: species.map_object_guid
                                      for guid, species in species_by_guid.items()}

    _reject_removed_fields_still_in_schema(prototype_schema)

    biomes: dict = {}
    for name in CURRENT_SCHEMA_BIOMES:
        converter = PrototypeConverter(map_object_guid_by_prefab_guid,
                                       name in STALE_SERIALIZED_BIOMES, REMOVED_UNITY_FIELDS)
        prototypes = [
            converter.convert(prototype_schema, prototype,
                              f"{name}.treePlacement.prototypes[{index}]")
            for index, prototype in enumerate(unity_biomes[name]["prototypes"])
        ]
        # disabled のプロトタイプは配置設定から除外する（樹種一覧には残す）
        # Disabled prototypes are dropped from the placement config but kept in the species list
        biomes[name.lower()] = {"prototypes": [p for p in prototypes if not p["disabled"]]}

    for name in LEGACY_SCHEMA_BIOMES:
        biomes[name.lower()] = {"speciesFill": [
            map_object_guid_by_prefab_guid[reference_guid(prototype["prefab"], f"{name}[{index}]")]
            for index, prototype in enumerate(unity_biomes[name]["prototypes"])
        ]}

    species = sorted(species_by_guid.values(), key=lambda entry: entry.key)
    document = {"species": [entry.to_json() for entry in species], "biomes": biomes}
    OUTPUT_PATH.write_text(json.dumps(document, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    _report(species, biomes)


def _prototype_schema() -> schema_spec.SchemaNode:
    config_schema = schema_spec.load_schema(SCHEMA_DIR, "treePlacementConfig")
    prototypes = dict(config_schema.properties)["prototypes"]
    return prototypes.item


def _reject_removed_fields_still_in_schema(prototype_schema) -> None:
    """削除済み扱いのフィールドがスキーマに現存していないことを検算する。"""
    """Verifies that fields treated as removed are genuinely absent from the schema."""
    resurrected = sorted(REMOVED_UNITY_FIELDS & {key for key, _ in prototype_schema.properties})
    if resurrected:
        raise ValueError(f"削除済みとみなしたフィールドがスキーマに存在する: {resurrected}")


def _load_tree_placement(biome_name: str) -> dict:
    asset = load_unity_asset(BIOME_PRESET_DIR / f"{biome_name}.asset")
    if "treePlacement" not in asset:
        raise KeyError(f"{biome_name}.asset: treePlacementが無い")
    return asset["treePlacement"]


def _collect_species(unity_biomes: dict, prefab_path_by_guid: dict) -> dict:
    """全プリセットのプロトタイプ参照から樹種を洗い出す（disabled分も登録する）。"""
    """Enumerates species from every preset's prototype references, disabled ones included."""
    species_by_guid: dict = {}
    for biome_name, tree_placement in unity_biomes.items():
        for index, prototype in enumerate(tree_placement["prototypes"]):
            location = f"{biome_name}.treePlacement.prototypes[{index}]"
            references = prototype["prefabs"] if "prefabs" in prototype else [prototype["prefab"]]
            for reference in references:
                guid = reference_guid(reference, location)
                if guid in species_by_guid:
                    continue
                if guid not in prefab_path_by_guid:
                    raise KeyError(f"{location}: guid {guid} のプレハブが非公開アセット内に見つからない")
                species_by_guid[guid] = species_catalog.build_species(guid, prefab_path_by_guid[guid])

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
        count = len(biome["prototypes"]) if "prototypes" in biome else len(biome["speciesFill"])
        label = "prototypes" if "prototypes" in biome else "speciesFill"
        print(f"{name}: {label} {count}")
    print(f"wrote {OUTPUT_PATH}")


if __name__ == "__main__":
    main()
