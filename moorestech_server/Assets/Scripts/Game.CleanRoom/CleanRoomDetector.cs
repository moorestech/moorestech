using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.CleanRoom
{
    public static class CleanRoomDetector
    {
        // 安全網。Cells 数（占有セル含む）に適用。
        // Safety net; applies to Cells count (incl. occupied cells).
        public const int MaxRoomVolume = 4096;

        // ワールド全体を走査し、密閉されたクリーンルームをすべて返す。
        // Scan the whole world and return all sealed clean rooms.
        public static List<CleanRoom> DetectAllRooms(IWorldBlockDatastore world)
        {
            BuildCellSets(world, out var boundaryCells, out var occupiedCells);
            var rooms = new List<CleanRoom>();
            if (boundaryCells.Count == 0) return rooms;

            ComputeAabb(boundaryCells, out var min, out var max);

            var globalVisited = new HashSet<Vector3Int>();
            var nextId = 0;

            // 境界セルの隣接セルを種として flood-fill を試みる。
            // Try flood-fill from each neighbor of boundary cells as a seed.
            foreach (var boundaryCell in boundaryCells)
            foreach (var seed in SixNeighbors(boundaryCell))
            {
                if (boundaryCells.Contains(seed)) continue;
                if (globalVisited.Contains(seed)) continue;
                if (IsOutsideAabb(seed, min, max)) continue;

                if (TryFloodFill(seed, out var cells, out var volume, out var surface))
                    rooms.Add(new CleanRoom(nextId++, cells, volume, surface));
            }

            return rooms;

            #region Internal

            // 種から通過セルを flood-fill。AABB外到達・リーク済み領域接触・上限超過で false。
            // Flood-fill passable cells; false on AABB exit, leaked-region contact, or cap overflow.
            bool TryFloodFill(Vector3Int start, out HashSet<Vector3Int> cells, out int volume, out int surfaceArea)
            {
                cells = new HashSet<Vector3Int>();
                volume = 0;
                surfaceArea = 0;
                var stack = new Stack<Vector3Int>();
                stack.Push(start);
                cells.Add(start);

                while (stack.Count > 0)
                {
                    var cur = stack.Pop();

                    if (cells.Count > MaxRoomVolume || IsOutsideAabb(cur, min, max)) return Fail(cells);

                    var isEmpty = !occupiedCells.Contains(cur);
                    if (isEmpty) volume++;

                    foreach (var n in SixNeighbors(cur))
                    {
                        if (boundaryCells.Contains(n))
                        {
                            if (isEmpty) surfaceArea++;
                            continue;
                        }
                        if (!cells.Contains(n) && globalVisited.Contains(n)) return Fail(cells);
                        if (cells.Add(n)) stack.Push(n);
                    }
                }

                foreach (var c in cells) globalVisited.Add(c);
                return true;
            }

            // 探索済みを visited 登録して不成立。
            // Register explored cells as visited and fail.
            bool Fail(HashSet<Vector3Int> cells)
            {
                foreach (var c in cells) globalVisited.Add(c);
                return false;
            }

            #endregion
        }

        // 全ブロックのセルを境界/占有（非境界）に分けて一括構築。
        // Build boundary/occupied cell sets in one pass over all blocks.
        private static void BuildCellSets(IWorldBlockDatastore world,
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

        private static void ComputeAabb(HashSet<Vector3Int> cells, out Vector3Int min, out Vector3Int max)
        {
            min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            foreach (var c in cells)
            {
                min = Vector3Int.Min(min, c);
                max = Vector3Int.Max(max, c);
            }
        }

        private static bool IsOutsideAabb(Vector3Int p, Vector3Int min, Vector3Int max)
        {
            return p.x < min.x || p.x > max.x || p.y < min.y || p.y > max.y || p.z < min.z || p.z > max.z;
        }

        private static IEnumerable<Vector3Int> SixNeighbors(Vector3Int p)
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
