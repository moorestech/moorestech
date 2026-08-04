using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Stages
{
    // 初期カメラとプレイヤーを配置物から守るため、スポーン周辺のmapObject候補を除外する。
    // Excludes map-object candidates around spawn to protect the initial camera and player.
    public static class SpawnPlacementExclusionStage
    {
        private const float SpawnClearance = 15f;

        public static void RemoveInsideSpawnClearance(List<PlacementEntry> entries, Vector3 spawnPoint)
        {
            // XZ平面の距離だけを使い、地表高やsink差で安全域をすり抜けさせない
            // Use XZ distance only so terrain height and sink differences cannot bypass the clearance
            var spawn = new Vector2(spawnPoint.x, spawnPoint.z);
            var clearanceSquared = SpawnClearance * SpawnClearance;

            for (var i = entries.Count - 1; 0 <= i; i--)
            {
                var position = entries[i].WorldPosition;
                var offset = new Vector2(position.x, position.z) - spawn;
                if (offset.sqrMagnitude < clearanceSquared) entries.RemoveAt(i);
            }
        }
    }
}
