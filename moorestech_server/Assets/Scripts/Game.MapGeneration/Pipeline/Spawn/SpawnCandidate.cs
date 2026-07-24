using System.Collections.Generic;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Spawn
{
    // 段1の候補（粗グリッド上、草原 CC 1つにつき1件）。
    // A stage-1 candidate (one per grassland CC on the coarse grid).
    internal sealed class SpawnCandidate
    {
        public Vector2 ApproxCenterWorld;
        public Vector2 BBoxCenterWorld;
        public float Score;
        public float BBoxW;
        public float BBoxH;
        public CoarseBiomeGrid SourceGrid;
        public List<int> Stage1GrassCells;
    }
}
