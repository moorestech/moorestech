using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.CleanRoom.Info.Leak
{
    /// <summary>
    ///     漏れ探索の共通処理（6方向・境界判定・境界までの軸レイ）
    ///     Shared leak-probe helpers: six directions, boundary checks and axis rays to the nearest boundary
    /// </summary>
    public static class CleanRoomLeakRays
    {
        // 軸レイが境界を探す最大距離（部屋の上限4096の一辺の目安）
        // Max distance an axis ray searches for a boundary (a rough side of the 4096-cell cap)
        public const int MaxRayLength = 64;

        public static readonly Vector3Int[] Directions =
        {
            new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1), new(0, 0, -1),
        };

        public static bool IsBoundary(IWorldBlockDatastore world, Vector3Int cell)
        {
            return world.TryGetBlock(cell, out var block) && block.ExistsComponent<ICleanRoomBoundaryComponent>();
        }

        // 始点の隣から direction へ進み、最初に当たった境界セルを返す
        // Walk from the origin's neighbor along direction and return the first boundary cell hit
        public static bool TryCastToBoundary(IWorldBlockDatastore world, Vector3Int origin, Vector3Int direction, out Vector3Int hit)
        {
            for (var step = 1; step <= MaxRayLength; step++)
            {
                var cell = origin + direction * step;
                if (!IsBoundary(world, cell)) continue;
                hit = cell;
                return true;
            }

            hit = origin;
            return false;
        }

        public static int AxisOf(Vector3Int direction)
        {
            return direction.x != 0 ? 0 : direction.y != 0 ? 1 : 2;
        }
    }
}
