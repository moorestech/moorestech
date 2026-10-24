using System.Collections.Generic;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.CleanRoom.Info.Leak
{
    /// <summary>
    ///     箱の面の外側1層が、まだ壁で囲われているかを調べる
    ///     Checks whether the layer just outside a box face is still enclosed by walls
    /// </summary>
    public static class CleanRoomLeakLayerTest
    {
        // 露出セルの外隣から層内4近傍に空きを塗り、箱の側面の縁へ届かなければ囲われている
        // Fill free cells in-layer from the exposed cells' outer neighbors; the layer is enclosed if none reaches the side rim
        public static bool IsLayerEnclosed(IWorldBlockDatastore world, CleanRoomLeakBox box, Vector3Int direction, IReadOnlyList<Vector3Int> exposedFaceCells)
        {
            var normalAxis = CleanRoomLeakRays.AxisOf(direction);
            var visited = new HashSet<Vector3Int>();
            var queue = new Queue<Vector3Int>();
            foreach (var faceCell in exposedFaceCells)
            {
                var start = faceCell + direction;
                if (visited.Add(start)) queue.Enqueue(start);
            }

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();

                // 縁に届いた空きは箱の外へ抜けているので、この層は囲われていない
                // A free cell on the rim escapes the box, so this layer is not enclosed
                if (box.IsOnLateralEdge(cell, direction)) return false;
                foreach (var inLayerDirection in CleanRoomLeakRays.Directions)
                {
                    if (CleanRoomLeakRays.AxisOf(inLayerDirection) == normalAxis) continue;
                    var next = cell + inLayerDirection;
                    if (visited.Contains(next) || CleanRoomLeakRays.IsBoundary(world, next)) continue;
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }

            return true;
        }
    }
}
