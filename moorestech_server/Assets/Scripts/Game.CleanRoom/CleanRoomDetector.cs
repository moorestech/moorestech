using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.CleanRoom
{
    public static class CleanRoomDetector
    {
        // 安全網。根拠: 大部屋例 V=500（10×10×5）の8倍超を許容しつつ、未密閉構造のリーク探索コストを抑える。
        // 大部屋戦略を殺さないかはプレイテストで再評価（バランス確定書§5）。Cells 数（占有セル含む）に適用。
        // Safety net (balance doc §5): allows >8x the large-room example V=500 while bounding leak-scan cost.
        public const int MaxRoomVolume = 4096;

        // ワールド全体を走査し、密閉されたクリーンルームをすべて返す。
        // Scan the whole world and return all sealed clean rooms.
        public static List<CleanRoom> DetectAllRooms(IWorldBlockDatastore world)
        {
            return DetectAllRooms(world, out _);
        }

        // 全走査版。fill で訪問した総セル数を out で返す（コスト計測・テスト用）。
        // Full scan; returns the total visited cell count via out (for cost measurement/tests).
        public static List<CleanRoom> DetectAllRooms(IWorldBlockDatastore world, out int visitedCellCount)
        {
            BuildCellSets(world, out var boundaryCells, out var occupiedCells);

            var rooms = new List<CleanRoom>();
            visitedCellCount = 0;
            if (boundaryCells.Count == 0) return rooms;

            // 境界セルの隣接セルを種に flood-fill を試みる。
            // Try flood-fill from each neighbor of every boundary cell as a seed.
            var seeds = new List<Vector3Int>();
            foreach (var boundaryCell in boundaryCells)
            foreach (var seed in SixNeighbors(boundaryCell))
                seeds.Add(seed);

            return DetectFromSeeds(seeds, boundaryCells, occupiedCells, 0, out visitedCellCount);
        }

        // 与えた種集合から局所的に密閉部屋を検出する。RebuildAll と差分更新の共通エンジン。
        // Detect sealed rooms locally from the given seeds; shared engine for full and incremental scans.
        // 通過セルなら自身を、境界セルなら通過する6近傍を起点に flood-fill する。
        // Passable seeds start from themselves; boundary seeds start from their passable 6-neighbors.
        public static List<CleanRoom> DetectFromSeeds(IReadOnlyList<Vector3Int> seeds,
            HashSet<Vector3Int> boundaryCells, HashSet<Vector3Int> occupiedCells,
            int firstRoomId, out int visitedCellCount)
        {
            var rooms = new List<CleanRoom>();
            visitedCellCount = 0;
            var globalVisited = new HashSet<Vector3Int>();
            var nextId = firstRoomId;

            foreach (var rawSeed in seeds)
            foreach (var start in StartCellsFor(rawSeed))
            {
                if (boundaryCells.Contains(start)) continue;
                if (globalVisited.Contains(start)) continue;

                if (TryFloodFill(start, out var cells, out var volume, out var surface, out var visited))
                    rooms.Add(new CleanRoom(nextId++, cells, volume, surface));
                visitedCellCount += visited;
            }

            return rooms;

            #region Internal

            // 種が境界セルなら通過する6近傍を、通過セルなら自身を起点として返す。
            // Yield passable neighbors of a boundary seed, or the seed itself if passable.
            IEnumerable<Vector3Int> StartCellsFor(Vector3Int seed)
            {
                if (!boundaryCells.Contains(seed))
                {
                    yield return seed;
                    yield break;
                }
                foreach (var n in SixNeighbors(seed))
                    if (!boundaryCells.Contains(n)) yield return n;
            }

            // 種から通過セルを flood-fill。触れた壁AABB体積を超える膨張・リーク済み接触・上限超過で false。
            // Flood-fill passable cells; false on growth past the touched-wall AABB volume, leaked-region contact, or cap.
            bool TryFloodFill(Vector3Int start, out HashSet<Vector3Int> cells, out int volume,
                out int surfaceArea, out int visited)
            {
                var cellSet = new HashSet<Vector3Int>();
                var vol = 0;
                var surface = 0;
                var visitedCount = 0;

                // 触れた壁AABB。密閉部屋の内部は壁に囲まれるため、最終的に必ずこの箱の内側へ収まる。
                // Touched-wall AABB; a sealed room's interior is wall-enclosed and always fits inside this box.
                var bboxInit = false;
                var bMin = Vector3Int.zero;
                var bMax = Vector3Int.zero;
                var leaked = false;

                var stack = new Stack<Vector3Int>();
                stack.Push(start);
                cellSet.Add(start);

                while (stack.Count > 0)
                {
                    var cur = stack.Pop();
                    visitedCount++;

                    var isEmpty = !occupiedCells.Contains(cur);
                    if (isEmpty) vol++;

                    // 触れた壁でAABBを成長させる（面接触の表面積も同時に数える）。
                    // Grow the AABB from touched walls (and count surface faces at the same time).
                    foreach (var n in SixNeighbors(cur))
                    {
                        if (!boundaryCells.Contains(n)) continue;
                        if (isEmpty) surface++;
                        if (!bboxInit) { bMin = n; bMax = n; bboxInit = true; }
                        else { bMin = Vector3Int.Min(bMin, n); bMax = Vector3Int.Max(bMax, n); }
                    }

                    // 上限超過、または触れた壁AABB体積を大きく超える膨張はリーク。
                    // Cap overflow, or growth well past the touched-wall AABB volume, means a leak.
                    // 密閉部屋の通過セル数は壁を含む外接箱体積以下。開放構造は壁を増やさず膨張し続けるため超過する。
                    // A sealed room's visited count never exceeds the wall-inclusive bbox volume; open structures keep growing.
                    if (cellSet.Count > MaxRoomVolume) { leaked = true; break; }
                    if (cellSet.Count > LeakVisitedLimit(bboxInit, bMin, bMax)) { leaked = true; break; }

                    foreach (var n in SixNeighbors(cur))
                    {
                        if (boundaryCells.Contains(n)) continue;
                        if (!cellSet.Contains(n) && globalVisited.Contains(n)) { leaked = true; break; }
                        if (cellSet.Add(n)) stack.Push(n);
                    }
                    if (leaked) break;
                }

                // 探索済みは visited 登録（リーク/成立どちらでも同一連結域の再探索を防ぐ）。
                // Register explored cells either way to avoid re-scanning the same connected region.
                foreach (var c in cellSet) globalVisited.Add(c);

                cells = cellSet;
                volume = vol;
                surfaceArea = surface;
                visited = visitedCount;
                return !leaked;
            }

            #endregion
        }

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

        // リーク前進ガードの定数: 触れた壁AABB体積に対する倍率と、bbox未成長時の前進床。
        // Leak forward-progress constants: a multiplier over the touched-wall AABB volume plus a floor for early fill.
        // 密閉部屋の通過セル数は壁を含む外接箱体積以下（順序非依存）。+1空間マージン方式の早期誤検知を避けるため体積基準にする。
        // A sealed room's visited count never exceeds the wall-inclusive bbox volume (order-independent); volume-based to avoid the +1-margin's premature false leaks.
        private const int LeakVolumeSlackMultiplier = 2;
        private const int LeakVisitedFloor = 64;

        // 壁未接触なら MaxRoomVolume のみ、接触後は 触れた壁AABB体積×倍率＋床 を許容上限とする。
        // Before any wall is touched only MaxRoomVolume applies; afterwards the limit is bbox-volume × multiplier + floor.
        private static int LeakVisitedLimit(bool bboxInit, Vector3Int min, Vector3Int max)
        {
            if (!bboxInit) return MaxRoomVolume;
            var dx = (long)(max.x - min.x + 1);
            var dy = (long)(max.y - min.y + 1);
            var dz = (long)(max.z - min.z + 1);
            var bboxVolume = dx * dy * dz;
            var limit = bboxVolume * LeakVolumeSlackMultiplier + LeakVisitedFloor;
            return limit > MaxRoomVolume ? MaxRoomVolume : (int)limit;
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
