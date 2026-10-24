using System.Collections.Generic;
using Game.CleanRoom.Util;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.CleanRoom.Info.Leak
{
    /// <summary>
    ///     箱の内側で種から6近傍の非境界セルを塗る。部屋の上限を1つ超えた時点で打ち切る
    ///     Fills non-boundary cells six-connected from the seeds inside the box, stopping one past the room cap
    /// </summary>
    public static class CleanRoomLeakFlood
    {
        public static HashSet<Vector3Int> FillInsideBox(IWorldBlockDatastore world, IReadOnlyList<Vector3Int> seeds, CleanRoomLeakBox box)
        {
            var visited = new HashSet<Vector3Int>();
            var queue = new Queue<Vector3Int>();
            foreach (var seed in seeds)
            {
                if (visited.Add(seed)) queue.Enqueue(seed);
            }

            while (queue.Count > 0 && visited.Count <= CleanRoomCellSets.MaxRoomVolume)
            {
                var cell = queue.Dequeue();
                foreach (var direction in CleanRoomLeakRays.Directions)
                {
                    var next = cell + direction;
                    if (!box.Contains(next) || visited.Contains(next) || CleanRoomLeakRays.IsBoundary(world, next)) continue;
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }

            return visited;
        }
    }
}
