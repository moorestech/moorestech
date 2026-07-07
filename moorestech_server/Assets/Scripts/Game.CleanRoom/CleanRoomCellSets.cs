using System;
using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.CleanRoom
{
    /// <summary>
    ///     密閉検出が使うセル集合の構築と、fill 予算計算のユーティリティ
    ///     Builds the cell sets used by detection and computes the fill budget
    /// </summary>
    public static class CleanRoomCellSets
    {
        public const int MaxRoomVolume = 4096;

        // 壁に触れる前の探索に許す初期予算
        // Initial exploration budget granted before any wall contact
        private const int LeakBoundBaseMargin = 64;

        public static void BuildCellSets(IWorldBlockDatastore world, out HashSet<Vector3Int> boundaryCells, out HashSet<Vector3Int> occupiedCells)
        {
            boundaryCells = new HashSet<Vector3Int>();
            occupiedCells = new HashSet<Vector3Int>();

            // 境界コンポーネント持ちは境界セル、それ以外のブロックは占有セルへ振り分ける
            // Boundary-component blocks go to boundaryCells; all other blocks to occupiedCells
            foreach (var blockData in world.BlockMasterDictionary.Values)
            {
                var isBoundary = blockData.Block.ComponentManager.ExistsComponent<ICleanRoomBoundaryComponent>();
                var targetCells = isBoundary ? boundaryCells : occupiedCells;

                var minPos = blockData.BlockPositionInfo.MinPos;
                var maxPos = blockData.BlockPositionInfo.MaxPos;
                for (var x = minPos.x; x <= maxPos.x; x++)
                for (var y = minPos.y; y <= maxPos.y; y++)
                for (var z = minPos.z; z <= maxPos.z; z++)
                    targetCells.Add(new Vector3Int(x, y, z));
            }
        }

        public static IEnumerable<Vector3Int> SixNeighbors(Vector3Int p)
        {
            yield return new Vector3Int(p.x + 1, p.y, p.z);
            yield return new Vector3Int(p.x - 1, p.y, p.z);
            yield return new Vector3Int(p.x, p.y + 1, p.z);
            yield return new Vector3Int(p.x, p.y - 1, p.z);
            yield return new Vector3Int(p.x, p.y, p.z + 1);
            yield return new Vector3Int(p.x, p.y, p.z - 1);
        }

        // 密閉部屋の内部セルは、その部屋を囲う壁の外接箱の内側に必ず収まる
        // Interior cells of a sealed room always fit inside the AABB of its enclosing walls
        // よって触れた壁のAABB体積×2+余白を超えて広がる fill は密閉ではあり得ずリークと断定できる
        // Hence a fill exceeding twice that AABB volume plus a margin cannot be sealed and is a leak
        public static int LeakVisitedLimit(bool bboxInitialized, Vector3Int min, Vector3Int max)
        {
            if (!bboxInitialized) return MaxRoomVolume;

            var size = max - min + Vector3Int.one;
            var bboxVolume = (long)size.x * size.y * size.z;
            return (int)Math.Min(bboxVolume * 2 + LeakBoundBaseMargin, MaxRoomVolume);
        }
    }
}
