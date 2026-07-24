using UnityEngine;

namespace Game.MapGeneration.Pipeline.Spawn
{
    // スポーン候補探索プリパスのパラメータ。面積は m² 単位（解像度非依存）。
    // Spawn candidate search parameters; areas are in m^2 (resolution-independent).
    public class SpawnSearchConfig
    {
        public bool overrideSpawnScenePosition = false;
        public Vector2 spawnScenePosition = new Vector2(500f, 500f);
        public float scanCellSize = 50f;
        public float scanExtent = 0f;
        public float windowMargin = 200f;
        public int maxDetailedResolution = 1600;
        public float minGrasslandArea = 200000f;
        public float minForestArea = 150000f;
        public float minBorderContact = 200f;
        public float grassClearanceMin = 30f;
        public float waterClearanceMin = 60f;
        public float wGrasslandArea = 1f;
        public float wForestArea = 0.5f;
        public float wBorderContact = 50f;
        public float wInland = 1f;
        public int topK = 32;
        public float expandFactor = 1.8f;
        public int maxExpandIterations = 4;
    }
}
