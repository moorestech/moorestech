using UnityEngine;

namespace Game.MapGeneration.Pipeline.Config
{
    // バイオームごとの岩石・小物配置設定。clusterEntries は階層配置、entries は独立散布。
    // Per-biome rock/prop placement; clusterEntries do hierarchical placement, entries do scatter.
    public class BiomeObjectConfig
    {
        // 独立散布エントリ（量は配置方式ごとのbandsで指定）。
        // Independent scatter entry; the quantity comes from the bands of its placement mode.
        public class ObjectEntry
        {
            public string[] mapObjectGuids;
            public TerrainSurroundEffectType terrainSurroundEffectType;

            // 配置方式ごとのパラメータ（距離帯もここが持つ）。
            // Per-mode placement parameters, which also carry the distance bands.
            public ObjectPlacementParam placement = new ObjectScatterParam();

            public Vector2 scaleRange = new Vector2(0.8f, 1.2f);
            public float slopeAlignment = 0f;
            public Vector2 sinkRange = Vector2.zero;
            public MapNoiseType noiseType = MapNoiseType.None;
            public float noiseFrequency = 10f;
            public float noiseAmplitude = 1f;
            public float noiseThreshold = 0.5f;
            public bool useSlopeFilter;
            public float slopeMin = 0f;
            public float slopeMax = 90f;
            public float slopeSmoothness = 4f;
            public float minDistanceFromTree;
            public float maxDistanceFromTree;
        }

        public ObjectClusterEntry[] clusterEntries = new ObjectClusterEntry[0];
        public ObjectEntry[] entries = new ObjectEntry[0];
        public ObjectAlgorithmConfig algorithmConfig = new ObjectAlgorithmConfig();
        public float borderMargin = 0f;
    }
}
