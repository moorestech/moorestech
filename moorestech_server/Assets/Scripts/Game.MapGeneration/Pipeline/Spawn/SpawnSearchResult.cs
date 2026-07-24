using UnityEngine;

namespace Game.MapGeneration.Pipeline.Spawn
{
    // SpawnRegionFinder.Find() の結果。座標はワールド(ノイズ)座標系(m)。
    // Result of SpawnRegionFinder.Find(); coordinates are in world (noise) space (m).
    public readonly struct SpawnSearchResult
    {
        public readonly bool Success;
        public readonly Vector2 WorldOffset;
        public readonly Vector2 SpawnWorldPosition;
        public readonly float Score;
        public readonly string Diagnostics;

        public SpawnSearchResult(bool success, Vector2 worldOffset, Vector2 spawn,
            float score, string diagnostics)
        {
            Success = success;
            WorldOffset = worldOffset;
            SpawnWorldPosition = spawn;
            Score = score;
            Diagnostics = diagnostics;
        }

        public static SpawnSearchResult Fallback(string reason) =>
            new SpawnSearchResult(false, Vector2.zero, Vector2.zero, 0f, reason);
    }
}
