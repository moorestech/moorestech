#!/usr/bin/env python3
"""species-inventory.jsonのbiomesでmaster generation.jsonのobjectConfigを同期し、generateObjectを有効化する（冪等）。

全8バイオームのobjectConfigをプリセット由来のものへ丸ごと差し替え、書き出し前にVanillaSchemaのbiomeObjectConfigと
キー単位で突合する。objectConfigが生成に効くには algorithmParam.generateObject が必要なので同時にtrueへ寄せる。

Replaces every biome's objectConfig with the preset-derived one and turns algorithmParam.generateObject on, since
objectConfig only takes effect through that flag. Before writing, each objectConfig is matched key by key against the
biomeObjectConfig schema.
"""
import copy
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from gen_generation_treeplacement import validate  # noqa: E402
from schema_spec import load_schema  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
INVENTORY = ROOT / "scripts/mapmaking-parity/species-inventory.json"
GENERATION = ROOT.parent / "moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/generation.json"
SCHEMA_DIR = ROOT / "VanillaSchema/mapGenerate"

# インベントリがobjectConfigを持つ全バイオーム。欠けていれば黙って残さず止める
# Every biome the inventory must carry an objectConfig for; a missing one stops instead of being left behind
BIOMES = ("grassland", "forest", "savanna", "desert", "mesa", "alpine", "jungle", "woods")


def main() -> None:
    inventory = json.loads(INVENTORY.read_text(encoding="utf-8"))
    generation = json.loads(GENERATION.read_text(encoding="utf-8"))
    schema = load_schema(SCHEMA_DIR, "biomeObjectConfig")
    algorithm_param = generation["algorithmParam"]

    missing = [biome for biome in BIOMES if "objectConfig" not in inventory["biomes"].get(biome, {})]
    if missing:
        raise ValueError(f"インベントリにobjectConfigが無いバイオーム: {missing}")

    for biome in BIOMES:
        object_config = copy.deepcopy(inventory["biomes"][biome]["objectConfig"])
        validate(schema, object_config, f"algorithmParam.{biome}.objectConfig")
        before = algorithm_param[biome]["objectConfig"]
        reject_dropped_map_objects(biome, before, object_config)
        algorithm_param[biome]["objectConfig"] = object_config
        print(f"{biome}: entries {len(before['entries'])} -> {len(object_config['entries'])}, "
              f"clusterEntries {len(before['clusterEntries'])} -> {len(object_config['clusterEntries'])}")

    # 移植元DefaultConfigはgenerateObject=1。falseのままだとObjectPlacementStageごと飛ばされ上の設定が死ぬ
    # The source DefaultConfig has generateObject=1; left false, the whole ObjectPlacementStage is skipped and the config above is dead
    print(f"generateObject: {algorithm_param['generateObject']} -> True")
    algorithm_param["generateObject"] = True

    GENERATION.write_text(json.dumps(generation, ensure_ascii=False, indent=2), encoding="utf-8")
    print("スキーマ突合OK / schema check passed")


def reject_dropped_map_objects(biome: str, before: dict, after: dict) -> None:
    """丸ごと差し替えでmaster側にしか無いmapObjectが消えるのを止める（インベントリ未収録の手追加entry対策）。
    Stops the wholesale replacement from dropping a mapObject that exists only on the master side (a hand-added entry the inventory never took in)."""
    dropped = sorted(_placed_map_object_guids(before) - _placed_map_object_guids(after))
    if dropped:
        raise ValueError(f"{biome}: インベントリに無いmapObjectがmaster側にある {dropped}。"
                         f"species-inventory.jsonへ収録してから再実行すること")


def _placed_map_object_guids(object_config: dict) -> set:
    guids = set()
    for entry in object_config["entries"]:
        guids.update(prefab["mapObjectGuid"] for prefab in entry["prefabs"])
    for cluster in object_config["clusterEntries"]:
        guids.update(prefab["mapObjectGuid"] for prefab in cluster["primary"])
        for secondary in cluster["secondaries"]:
            guids.update(prefab["mapObjectGuid"] for prefab in secondary["prefabs"])
    return guids


if __name__ == "__main__":
    main()
