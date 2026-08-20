#!/usr/bin/env python3
"""species-inventory.jsonのbiomesでmaster generation.jsonのtreePlacementを同期する（冪等）。

forest/grassland/savanna/mesaはprototypesを丸ごと差し替え、jungle/woodsは配置パラメータを残して
mapObjectsだけをspeciesFillの全guid（等確率）へ置き換える。書き出し前に全バイオームのprototypesを
VanillaSchemaのtreePlacementConfigとキー単位で突合し、欠落・余剰があれば例外で止める。

Replaces the whole prototypes array for forest/grassland/savanna/mesa and, for jungle/woods, swaps only
mapObjects with every speciesFill guid while keeping the placement parameters.
Before writing, every biome's prototypes are matched key by key against the treePlacementConfig schema and
any missing or extra key raises.
"""
import copy
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from schema_spec import ARRAY, ENUM, OBJECT, SchemaNode, load_schema  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
INVENTORY = ROOT / "scripts/mapmaking-parity/species-inventory.json"
GENERATION = ROOT.parent / "moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/generation.json"
SCHEMA_DIR = ROOT / "VanillaSchema/mapGenerate"

# プリセットのprototypesをそのまま採用するバイオーム / Biomes whose prototypes are taken from the preset as-is
PRESET_BIOMES = ("forest", "grassland", "savanna", "mesa")
# 樹種リストだけ採用し配置パラメータは既存を残すバイオーム / Biomes that adopt only the species list
SPECIES_FILL_BIOMES = ("jungle", "woods")
# prototype内でplacementNoiseが現れる位置 / Where placementNoise appears inside a prototype
NOISE_PATHS = (("slopeFilter", "noise"), ("curvatureFilter", "noise"), ("clusterNoise",), ("clusterNoise2",))


def replace_prototypes(generation: dict, biome: str, prototypes: list) -> str:
    """プリセット由来のprototypesで既存配列を丸ごと置き換える。
    Swaps the whole prototypes array with the preset-derived one."""
    tree_placement = generation["algorithmParam"][biome]["treePlacement"]
    before = len(tree_placement["prototypes"])
    tree_placement["prototypes"] = copy.deepcopy(prototypes)
    return f"{biome}: prototypes {before} -> {len(prototypes)} (全置換 / full replace)"


def fill_species(generation: dict, biome: str, species_fill: list) -> str:
    """既存prototypeの配置パラメータを保ったままmapObjectsだけを樹種guid全件へ置き換える。
    Replaces only mapObjects with every species guid, keeping each prototype's placement parameters."""
    prototypes = generation["algorithmParam"][biome]["treePlacement"]["prototypes"]
    map_objects = [{"mapObjectGuid": guid} for guid in species_fill]
    for prototype in prototypes:
        prototype["mapObjects"] = copy.deepcopy(map_objects)
    return f"{biome}: prototypes {len(prototypes)} × mapObjects {len(map_objects)} (樹種のみ差し替え / species only)"


def complete_texture_png_path(generation: dict, biome: str) -> int:
    """placementNoiseの必須キーtexturePngPathを、テクスチャ源が無い意味の空文字で明示する。
    States the required placementNoise key texturePngPath explicitly as an empty string when there is no texture."""
    filled = 0
    for prototype in generation["algorithmParam"][biome]["treePlacement"]["prototypes"]:
        for path in NOISE_PATHS:
            noise = prototype
            for key in path:
                noise = noise[key]
            if "texturePngPath" not in noise:
                noise["texturePngPath"] = ""
                filled += 1
    return filled


def validate(node: SchemaNode, value, location: str) -> None:
    """スキーマとJSONをキー単位で突合し、欠落・余剰・列挙外の値を例外で止める。
    Matches JSON against the schema key by key and raises on missing, extra, or out-of-enum values."""
    if node.kind == OBJECT:
        if not isinstance(value, dict):
            raise ValueError(f"{location}: objectであるべきだが {type(value).__name__}")
        expected = {key for key, _ in node.properties}
        actual = set(value)
        if expected != actual:
            missing = sorted(expected - actual)
            extra = sorted(actual - expected)
            raise ValueError(f"{location}: キー不一致 missing={missing} extra={extra}")
        for key, child in node.properties:
            validate(child, value[key], f"{location}.{key}")
        return

    if node.kind == ARRAY:
        if not isinstance(value, list):
            raise ValueError(f"{location}: arrayであるべきだが {type(value).__name__}")
        for index, item in enumerate(value):
            validate(node.item, item, f"{location}[{index}]")
        return

    if node.kind == ENUM and value not in node.options:
        raise ValueError(f"{location}: 列挙外の値 {value!r} (options={node.options})")


def main() -> None:
    inventory = json.loads(INVENTORY.read_text(encoding="utf-8"))
    generation = json.loads(GENERATION.read_text(encoding="utf-8"))
    biomes = inventory["biomes"]
    schema = load_schema(SCHEMA_DIR, "treePlacementConfig")

    # インベントリのバイオームが想定の分類から外れていたら、黙って一部を落とさず止める
    # Stop instead of silently skipping a biome the inventory carries but this script does not classify
    unknown = set(biomes) - set(PRESET_BIOMES) - set(SPECIES_FILL_BIOMES)
    if unknown:
        raise ValueError(f"分類されていないバイオーム: {sorted(unknown)}")

    summary = []
    for biome in PRESET_BIOMES:
        summary.append(replace_prototypes(generation, biome, biomes[biome]["prototypes"]))
    for biome in SPECIES_FILL_BIOMES:
        summary.append(fill_species(generation, biome, biomes[biome]["speciesFill"]))

    # 触れないバイオームにも必須キーの欠落が残るとマスタのフルロードが落ちるので補って明示する
    # Untouched biomes would still break the master's full load with the key missing, so state it there too
    filled = {biome: complete_texture_png_path(generation, biome)
              for biome in generation["algorithmParam"]
              if isinstance(generation["algorithmParam"][biome], dict)
              and "treePlacement" in generation["algorithmParam"][biome]}

    for biome, value in generation["algorithmParam"].items():
        if isinstance(value, dict) and "treePlacement" in value:
            validate(schema, value["treePlacement"], f"algorithmParam.{biome}.treePlacement")

    GENERATION.write_text(json.dumps(generation, ensure_ascii=False, indent=2), encoding="utf-8")
    for line in summary:
        print(line)
    filled_report = ", ".join(f"{k}={v}" for k, v in filled.items() if v)
    print("texturePngPath補完 / filled: " + (filled_report or "なし / none"))
    print("スキーマ突合OK / schema check passed")


if __name__ == "__main__":
    main()
