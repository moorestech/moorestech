using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline.Biomes
{
    // バイオーム別の非ハイトマップ設定取得を集約するヘルパー。見た目系（TerrainLayer/
    // TreePrototype/Texture/Detail）はサーバーでは扱わないため除外した。
    // Helper aggregating per-biome non-heightmap config access; view-only accessors
    // (TerrainLayer/TreePrototype/Texture/Detail) are excluded on the server.
    public class BiomePlacementHelper
    {
        readonly TerrainGenerationConfig _config;

        public BiomePlacementHelper(TerrainGenerationConfig config)
        {
            _config = config;
        }

        // スプラットマップ列インデックス（バイオーム順の固定割り当て）。
        // Splatmap column index (fixed per-biome assignment).
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

        // バイオーム別の樹木配置設定。
        // Per-biome tree placement config.
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

        // guid → 最初に出会った TreePrototypeEntry。有効バイオーム順・エントリ順・guid順で最初の出現が勝つ。
        // これは元の prefab 展開＋prefabToProtoIndex（最初のインデックス）による属性付けと一致する。
        // 高さ摂動と根元塗りが別々にこの規約を書くと、同じ guid が2つの entry に載る mod で別の属性を引く。
        // Maps each guid to the TreePrototypeEntry it first meets in enabled-biome, entry then guid order,
        // matching the original prefab expansion plus prefabToProtoIndex (first index) attribution.
        // Restating the rule per consumer would let the height perturbation and the root painting
        // read different entries for one guid when a mod lists it in two prototypes.
        public Dictionary<string, TreePrototypeEntry> BuildFirstTreePrototypeByGuid(BiomeType[] biomeTypes)
        {
            var firstEntryByGuid = new Dictionary<string, TreePrototypeEntry>();
            foreach (var biome in biomeTypes)
            {
                var treePlacement = GetTreePlacementConfig(biome);
                if (treePlacement?.prototypes == null) continue;

                foreach (var entry in treePlacement.prototypes)
                {
                    if (entry == null || entry.disabled || entry.mapObjectGuids == null) continue;

                    foreach (var mapObjectGuid in entry.mapObjectGuids)
                    {
                        if (string.IsNullOrEmpty(mapObjectGuid) || firstEntryByGuid.ContainsKey(mapObjectGuid)) continue;
                        firstEntryByGuid[mapObjectGuid] = entry;
                    }
                }
            }
            return firstEntryByGuid;
        }

        // バイオーム別のオブジェクト配置設定。
        // Per-biome object placement config.
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

        public ObjectAlgorithmConfig GetObjectAlgorithmConfig(BiomeType biome)
        {
            var oc = GetObjectConfig(biome);
            return oc?.algorithmConfig ?? new ObjectAlgorithmConfig();
        }

        // 海岸設定・境界設定は全バイオーム共通（TerrainGenerationConfig 側で一元管理）。
        // Shore/boundary configs are shared across biomes (managed on TerrainGenerationConfig).
        public BiomeShoreConfig GetShoreConfig(BiomeType biome)
        {
            return _config.shoreConfig ?? new BiomeShoreConfig();
        }

        public BiomeBoundaryConfig GetBoundaryConfig()
        {
            return _config.boundaryConfig;
        }
    }
}
