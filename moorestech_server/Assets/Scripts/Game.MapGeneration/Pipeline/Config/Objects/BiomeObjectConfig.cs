using UnityEngine;

namespace Game.MapGeneration.Pipeline.Config
{
    // バイオームごとの岩石・小物配置設定。clusterEntries は階層配置、entries は独立散布。
    // Per-biome rock/prop placement; clusterEntries do hierarchical placement, entries do scatter.
    public class BiomeObjectConfig
    {
        // 独立散布エントリ（prefabs は mapObjectGuid 配列へ置換）。
        // Independent scatter entry (prefabs replaced by mapObjectGuid array).
        public class ObjectEntry
        {
            public string[] mapObjectGuids;
            public float density = 1f;
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
            public bool useClusterMode;
            public int clusterCount = 8;
            public int objectsPerCluster = 4;
            public float clusterRadius = 12f;
            public float minDistanceFromTree;
            public float maxDistanceFromTree;
        }

        public ObjectClusterEntry[] clusterEntries = new ObjectClusterEntry[0];
        public ObjectEntry[] entries = new ObjectEntry[0];
        public ObjectAlgorithmConfig algorithmConfig = new ObjectAlgorithmConfig();
        public float borderMargin = 0f;
    }
}
