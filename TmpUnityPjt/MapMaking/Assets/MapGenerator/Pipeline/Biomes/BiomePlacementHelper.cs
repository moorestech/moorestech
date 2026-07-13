using System;
using System.Collections.Generic;
using MapGenerator.Pipeline.Config;
using MapGenerator.Pipeline.Generators.Util;
using UnityEngine;

namespace MapGenerator.Pipeline.Biomes
{
    /// <summary>
    /// 全バイオームの非ハイトマップ系配置ロジックを集約するマネージドクラス。
    /// Unity Object（TreePrototype, TerrainLayer, Prefab等）を扱うためBurst不可。
    /// TerrainData用のプロトタイプ配列生成とバイオーム別Config取得を担当。
    /// </summary>
    public class BiomePlacementHelper
    {
        readonly TerrainGenerationConfig _config;

        public BiomePlacementHelper(TerrainGenerationConfig config)
        {
            _config = config;
        }

        // --- SplatmapLayerIndex ---
        public int GetSplatmapLayerIndex(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.Grassland: return 1;
                case BiomeType.Forest:    return 2;
                case BiomeType.Savanna:   return 3;
                case BiomeType.Desert:    return 4;
                case BiomeType.Mesa:      return 5;
                case BiomeType.Alpine:    return 6;
                case BiomeType.Jungle:    return 7;
                case BiomeType.Woods:     return 8;
                default: return 0;
            }
        }

        // --- TerrainLayer ---
        public TerrainLayer GetTerrainLayer(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.Grassland: return _config.grassland.terrainLayer;
                case BiomeType.Forest:    return _config.forest.terrainLayer;
                case BiomeType.Savanna:   return _config.savanna.terrainLayer;
                case BiomeType.Desert:    return _config.desert.terrainLayer;
                case BiomeType.Mesa:      return _config.mesa.terrainLayer;
                case BiomeType.Alpine:    return _config.alpine.terrainLayer;
                case BiomeType.Jungle:    return _config.jungle.terrainLayer;
                case BiomeType.Woods:     return _config.woods.terrainLayer;
                default: return null;
            }
        }

        // --- TreePlacementConfig ---
        public TreePlacementConfig GetTreePlacementConfig(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.Grassland: return _config.grassland.treePlacement;
                case BiomeType.Forest:    return _config.forest.treePlacement;
                case BiomeType.Savanna:   return _config.savanna.treePlacement;
                case BiomeType.Desert:    return _config.desert.treePlacement;
                case BiomeType.Mesa:      return _config.mesa.treePlacement;
                case BiomeType.Alpine:    return _config.alpine.treePlacement;
                case BiomeType.Jungle:    return _config.jungle.treePlacement;
                case BiomeType.Woods:     return _config.woods.treePlacement;
                default: return null;
            }
        }

        // --- TreePrototypes ---
        // TreePrototypeEntry配列からTreePrototype[]に変換（TerrainData用）
        public TreePrototype[] GetTreePrototypes(BiomeType biome)
        {
            var tp = GetTreePlacementConfig(biome);
            if (tp?.prototypes == null || tp.prototypes.Length == 0)
                return Array.Empty<TreePrototype>();

            var result = new List<TreePrototype>();
            foreach (var entry in tp.prototypes)
            {
                if (entry == null || entry.disabled || entry.prefabs == null) continue;
                foreach (var go in entry.prefabs)
                {
                    if (go == null) continue;
                    result.Add(new TreePrototype
                    {
                        prefab = go,
                        bendFactor = entry.bendFactor
                    });
                }
            }
            return result.ToArray();
        }

        // --- 有効プロトタイプ配列（TreeInstance→Entry解決用）---
        // 1 Entryが複数prefabsを持つ場合、prefab数分だけ同じEntryを繰り返す
        public TreePrototypeEntry[] GetActivePrototypeEntries(BiomeType biome)
        {
            var tp = GetTreePlacementConfig(biome);
            if (tp?.prototypes == null) return Array.Empty<TreePrototypeEntry>();
            var result = new List<TreePrototypeEntry>();
            foreach (var entry in tp.prototypes)
            {
                if (entry == null || entry.disabled || entry.prefabs == null) continue;
                foreach (var go in entry.prefabs)
                    if (go != null) result.Add(entry);
            }
            return result.ToArray();
        }

        // --- TextureConfig ---
        public BiomeTextureConfig GetTextureConfig(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.Grassland: return _config.grassland.textureConfig;
                case BiomeType.Forest:    return _config.forest.textureConfig;
                case BiomeType.Savanna:   return _config.savanna.textureConfig;
                case BiomeType.Desert:    return _config.desert.textureConfig;
                case BiomeType.Mesa:      return _config.mesa.textureConfig;
                case BiomeType.Alpine:    return _config.alpine.textureConfig;
                case BiomeType.Jungle:    return _config.jungle.textureConfig;
                case BiomeType.Woods:     return _config.woods.textureConfig;
                default: return new BiomeTextureConfig();
            }
        }

        // --- DetailConfig ---
        public BiomeDetailConfig GetDetailConfig(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.Grassland: return _config.grassland.detailConfig;
                case BiomeType.Forest:    return _config.forest.detailConfig;
                case BiomeType.Savanna:   return _config.savanna.detailConfig;
                case BiomeType.Desert:    return _config.desert.detailConfig;
                case BiomeType.Mesa:      return _config.mesa.detailConfig;
                case BiomeType.Alpine:    return _config.alpine.detailConfig;
                case BiomeType.Jungle:    return _config.jungle.detailConfig;
                case BiomeType.Woods:     return _config.woods.detailConfig;
                default: return new BiomeDetailConfig();
            }
        }

        // --- ObjectConfig ---
        public BiomeObjectConfig GetObjectConfig(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.Grassland: return _config.grassland.objectConfig;
                case BiomeType.Forest:    return _config.forest.objectConfig;
                case BiomeType.Savanna:   return _config.savanna.objectConfig;
                case BiomeType.Desert:    return _config.desert.objectConfig;
                case BiomeType.Mesa:      return _config.mesa.objectConfig;
                case BiomeType.Alpine:    return _config.alpine.objectConfig;
                case BiomeType.Jungle:    return _config.jungle.objectConfig;
                case BiomeType.Woods:     return _config.woods.objectConfig;
                default: return new BiomeObjectConfig();
            }
        }

        // 鉱脈はワールド全体で一元管理する（TerrainGenerationConfig.oreConfig）。
        // バイオーム別の GetOreConfig は廃止した。

        // --- アルゴリズム設定アクセサ ---
        public ObjectAlgorithmConfig GetObjectAlgorithmConfig(BiomeType biome)
        {
            var oc = GetObjectConfig(biome);
            return oc?.algorithmConfig ?? new ObjectAlgorithmConfig();
        }

        public ObjectSurroundTextureConfig GetSurroundTextureConfig(BiomeType biome)
        {
            var oc = GetObjectConfig(biome);
            return oc?.surroundTextureConfig ?? new ObjectSurroundTextureConfig();
        }

        // --- 海岸設定 ---
        public BiomeShoreConfig GetShoreConfig(BiomeType biome)
        {
            return _config.shoreConfig ?? new BiomeShoreConfig();
        }

        // --- 境界設定（全バイオーム共通） ---
        public BiomeBoundaryConfig GetBoundaryConfig()
        {
            return _config.boundaryConfig;
        }
    }
}
