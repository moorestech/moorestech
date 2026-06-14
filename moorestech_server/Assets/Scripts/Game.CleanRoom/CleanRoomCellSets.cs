using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.CleanRoom
{
    // 検出エンジン(CleanRoomDetector)が使う、境界/占有セル集合の構築と flood-fill のリーク前進ガード。
    // Cell-set building and the flood-fill leak/forward-progress guard used by the CleanRoomDetector engine.
    public static class CleanRoomCellSets
    {
        // 安全網。根拠: 大部屋例 V=500（10×10×5）の8倍超を許容しつつ、未密閉構造のリーク探索コストを抑える。
        // 大部屋戦略を殺さないかはプレイテストで再評価（バランス確定書§5）。Cells 数（占有セル含む）に適用。
        // Safety net (balance doc §5): allows >8x the large-room example V=500 while bounding leak-scan cost. Applied to the Cells count.
        public const int MaxRoomVolume = 4096;

        // リーク前進ガードの定数: 触れた壁AABB体積に対する倍率と、bbox未成長時の前進床。
        // 密閉部屋の通過セル数は壁を含む外接箱体積以下（順序非依存）。+1空間マージン方式の早期誤検知を避けるため体積基準にする。
        // Leak forward-progress constants: a multiplier over the touched-wall AABB volume plus a floor for early fill (volume-based to avoid the +1-margin's premature false leaks).
        private const int LeakVolumeSlackMultiplier = 2;
        private const int LeakVisitedFloor = 64;

        // 全ブロックのセルを境界/占有（非境界）に分けて一括構築。
        // Build boundary/occupied cell sets in one pass over all blocks.
        public static void BuildCellSets(IWorldBlockDatastore world,
            out HashSet<Vector3Int> boundaryCells, out HashSet<Vector3Int> occupiedCells)
        {
            boundaryCells = new HashSet<Vector3Int>();
            occupiedCells = new HashSet<Vector3Int>();
            foreach (var kvp in world.BlockMasterDictionary)
            {
                var data = kvp.Value;
                var isBoundary = data.Block.TryGetComponent<ICleanRoomBoundaryComponent>(out _);
                var target = isBoundary ? boundaryCells : occupiedCells;

                var info = data.BlockPositionInfo;
                for (var x = info.MinPos.x; x <= info.MaxPos.x; x++)
                for (var y = info.MinPos.y; y <= info.MaxPos.y; y++)
                for (var z = info.MinPos.z; z <= info.MaxPos.z; z++)
                    target.Add(new Vector3Int(x, y, z));
            }
        }

        // 壁未接触なら MaxRoomVolume のみ、接触後は 触れた壁AABB体積×倍率＋床 を許容上限とする。
        // Before any wall is touched only MaxRoomVolume applies; afterwards the limit is bbox-volume × multiplier + floor.
        public static int LeakVisitedLimit(bool bboxInit, Vector3Int min, Vector3Int max)
        {
            if (!bboxInit) return MaxRoomVolume;
            var dx = (long)(max.x - min.x + 1);
            var dy = (long)(max.y - min.y + 1);
            var dz = (long)(max.z - min.z + 1);
            var bboxVolume = dx * dy * dz;
            var limit = bboxVolume * LeakVolumeSlackMultiplier + LeakVisitedFloor;
            return limit > MaxRoomVolume ? MaxRoomVolume : (int)limit;
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
    }
}
