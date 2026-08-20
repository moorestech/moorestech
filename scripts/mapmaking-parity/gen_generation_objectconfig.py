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
        algorithm_param[biome]["objectConfig"] = object_config
        print(f"{biome}: entries {len(before['entries'])} -> {len(object_config['entries'])}, "
              f"clusterEntries {len(before['clusterEntries'])} -> {len(object_config['clusterEntries'])}")

    # 移植元DefaultConfigはgenerateObject=1。falseのままだとObjectPlacementStageごと飛ばされ上の設定が死ぬ
    # The source DefaultConfig has generateObject=1; left false, the whole ObjectPlacementStage is skipped and the config above is dead
    print(f"generateObject: {algorithm_param['generateObject']} -> True")
    algorithm_param["generateObject"] = True

    GENERATION.write_text(json.dumps(generation, ensure_ascii=False, indent=2), encoding="utf-8")
    print("スキーマ突合OK / schema check passed")


if __name__ == "__main__":
    main()
