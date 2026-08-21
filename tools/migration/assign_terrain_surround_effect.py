#!/usr/bin/env python3
"""map.json の mapObject ごとの terrainSurroundEffectType を、generation.json の配置エントリへ移す一回限りの変換。
entry 内の prefab が異なる種別を混在させていたら例外で止める（手で裁定する）。"""
import json, sys
from collections import Counter
gen_path, map_path = sys.argv[1], sys.argv[2]
map_json = json.load(open(map_path, encoding='utf-8'))
objects = map_json if isinstance(map_json, list) else next(v for v in map_json.values() if isinstance(v, list))
kind_by_guid = {o['mapObjectGuid']: o['terrainSurroundEffectType'] for o in objects}
gen = json.load(open(gen_path, encoding='utf-8'))
param = gen['algorithmParam']

def decide(guids, path, allowed, default):
    kinds = Counter(kind_by_guid[g] for g in guids if g in kind_by_guid)
    if len(kinds) > 1: raise SystemExit(f"混在: {path} {dict(kinds)}")
    kind = next(iter(kinds), default)
    if kind not in allowed: raise SystemExit(f"許可外: {path} {kind}")
    return kind

for biome in ['grassland','forest','savanna','desert','mesa','alpine','jungle','woods']:
    section = param[biome]
    for i, p in enumerate(section.get('treePlacement', {}).get('prototypes', []) or []):
        guids = [m['mapObjectGuid'] for m in p.get('mapObjects', [])]
        p['terrainSurroundEffectType'] = decide(guids, f'{biome}.treePlacement.prototypes[{i}]', {'treeRootPatch','rockNoBareGround'}, 'treeRootPatch')
    oc = section.get('objectConfig', {}) or {}
    for i, e in enumerate(oc.get('entries', []) or []):
        guids = [m['mapObjectGuid'] for m in e.get('prefabs', [])]
        e['terrainSurroundEffectType'] = decide(guids, f'{biome}.objectConfig.entries[{i}]', {'rockBareGround','rockNoBareGround'}, 'rockNoBareGround')
    for i, ce in enumerate(oc.get('clusterEntries', []) or []):
        guids = [m['mapObjectGuid'] for m in ce.get('primary', [])]
        ce['terrainSurroundEffectType'] = decide(guids, f'{biome}.objectConfig.clusterEntries[{i}]', {'rockBareGround','rockNoBareGround'}, 'rockNoBareGround')
        for j, s in enumerate(ce.get('secondaries', []) or []):
            guids = [m['mapObjectGuid'] for m in s.get('prefabs', [])]
            s['terrainSurroundEffectType'] = decide(guids, f'{biome}.objectConfig.clusterEntries[{i}].secondaries[{j}]', {'rockBareGround','rockNoBareGround'}, 'rockNoBareGround')

json.dump(gen, open(gen_path, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
open(gen_path, 'a', encoding='utf-8').write('\n')
print('ok', gen_path)
