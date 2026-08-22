"""
MapMakingのbiomeプリセットからobjectConfig（岩・小物の配置設定）をbiomeObjectConfigスキーマ準拠のdictへ抽出する。
Extracts objectConfig (rock and prop placement) from MapMaking biome presets into dicts that follow the biomeObjectConfig schema.
"""

from __future__ import annotations

import schema_spec
from prototype_converter import PrototypeConverter
from unity_asset_yaml import reference_guid

# 移植元TerrainGenerator.ApplyObjectSurroundTextureはsurroundLayer未設定時に「名前にMudを含む最初のTerrainLayer」へ
# フォールバックする。5x5プリセットのレイヤー順ではMudDryが最初のMudなので、そのアドレスを正本として写す
# The source TerrainGenerator.ApplyObjectSurroundTexture falls back to "the first TerrainLayer whose name contains Mud" when
# surroundLayer is unset; in the 5x5 preset's layer order that is MudDry, so its address is transcribed as the truth
OBJECT_SURROUND_FALLBACK_ADDRESS = "Vanilla/Environment/Terrain/Layer/Oasis/MudDry"

# BiomeObjectConfig改修後に再保存されておらず、clusterEntries等が未記録・ObjectEntryの旧フィールドが残るプリセット
# Presets not re-saved since the BiomeObjectConfig rework: clusterEntries etc. unwritten, legacy ObjectEntry fields still present
STALE_OBJECT_CONFIG_BIOMES = frozenset({"Mesa", "Alpine", "Jungle", "Woods"})

# 現行ObjectEntryから削除済みでUnityが読み捨てるフィールド（Mesaのみ残存・全て無効値）
# Fields removed from the current ObjectEntry that Unity discards (left only in Mesa, all inert)
REMOVED_OBJECT_ENTRY_FIELDS = frozenset({"role", "countPerCluster", "minParentDistance", "maxParentDistance"})

# 未再保存プリセットに欠けており、スキーマ既定値（C#初期値）で補うことを許すフィールド
# Fields absent from stale presets that may be filled from the schema default (the C# initializer)
STALE_MISSING_OBJECT_FIELDS = frozenset({"clusterEntries", "algorithmConfig", "surroundTextureConfig", "borderMargin"})

# 移植元ObjectEntryは距離帯を持たず、量をflatなdensity（1haあたり）とclusterCount（クラスタモード時の個数）で持つ
# The source ObjectEntry has no distance bands: it holds the amount as a flat density (per hectare) plus clusterCount in cluster mode
LEGACY_AMOUNT_FIELDS = frozenset({"density", "clusterCount"})

# clusterCount（タイル1枚あたりの個数）を1haあたりのdensityへ直す係数。タイル1000x1000m = 100ha
# Converts clusterCount (per 1000x1000m tile) into a per-hectare density; one tile is 100 hectares
CLUSTER_COUNT_PER_HECTARE = 100.0


class ObjectConfigConverter(PrototypeConverter):
    """移植元のflatな量指定を、現行スキーマの単一無限バンドへ畳んで写す変換器。
    Converter folding the source's flat amount fields into the current schema's single infinite band."""

    def _derived_field_value(self, key, node, unity_value, location):
        if key != "bands":
            return None

        missing = sorted(LEGACY_AMOUNT_FIELDS - set(unity_value))
        if missing:
            raise KeyError(f"{location}: bandsへ畳む旧フィールドが無い {missing}")

        # クラスタモードは中心数（clusterCount）、非クラスタは点数（density）が量。density統一の換算はADR-0027参照
        # In cluster mode the amount is the centre count (clusterCount); otherwise it is the point count (density). See ADR-0027 for the unification
        cluster_mode = _require_boolean(unity_value, "useClusterMode", location)
        density = unity_value["clusterCount"] / CLUSTER_COUNT_PER_HECTARE if cluster_mode \
            else unity_value["density"]
        return [{"outerRadiusMeters": -1, "density": density}], LEGACY_AMOUNT_FIELDS


def _require_boolean(unity_value: dict, key: str, location: str) -> bool:
    value = unity_value.get(key)
    if value not in (0, 1):
        raise ValueError(f"{location}: {key} は0/1のみ許容 ({value!r})")
    return bool(value)


def object_config_schema(schema_dir) -> schema_spec.SchemaNode:
    return schema_spec.load_schema(schema_dir, "biomeObjectConfig")


def iter_object_prefab_references(object_config: dict, biome_name: str):
    """objectConfigが参照する全プレハブ参照を (guid, location) で列挙する。
    Enumerates every prefab reference in an objectConfig as (guid, location)."""
    for index, entry in enumerate(object_config.get("entries", [])):
        for ref_index, reference in enumerate(entry["prefabs"]):
            location = f"{biome_name}.objectConfig.entries[{index}].prefabs[{ref_index}]"
            yield reference_guid(reference, location), location
    for index, cluster in enumerate(object_config.get("clusterEntries", [])):
        for ref_index, reference in enumerate(cluster["primary"]):
            location = f"{biome_name}.objectConfig.clusterEntries[{index}].primary[{ref_index}]"
            yield reference_guid(reference, location), location
        for sec_index, secondary in enumerate(cluster["secondaries"]):
            for ref_index, reference in enumerate(secondary["prefabs"]):
                location = (f"{biome_name}.objectConfig.clusterEntries[{index}]"
                            f".secondaries[{sec_index}].prefabs[{ref_index}]")
                yield reference_guid(reference, location), location


def convert_object_config(schema: schema_spec.SchemaNode, object_config: dict, biome_name: str,
                          map_object_guid_by_prefab_guid: dict) -> dict:
    """1バイオームのobjectConfigをスキーマ順dictへ写す。未再保存プリセットは宣言済みの欠落・残存のみ許す。
    Transcribes one biome's objectConfig into a schema-ordered dict; stale presets get only the declared gaps and leftovers."""
    stale = biome_name in STALE_OBJECT_CONFIG_BIOMES
    converter = ObjectConfigConverter(map_object_guid_by_prefab_guid, stale,
                                   REMOVED_OBJECT_ENTRY_FIELDS, STALE_MISSING_OBJECT_FIELDS,
                                   OBJECT_SURROUND_FALLBACK_ADDRESS)
    converted = converter.convert(schema, object_config, f"{biome_name}.objectConfig")
    if stale:
        converter.reject_missing_field_mismatch(f"{biome_name}.objectConfig")
    return converted
