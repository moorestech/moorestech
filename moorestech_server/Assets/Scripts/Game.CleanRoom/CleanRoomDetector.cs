using System.Collections.Generic;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.CleanRoom
{
    // 密閉部屋の flood-fill 検出エンジン。セル集合の構築とリーク判定は CleanRoomCellSets に委譲する。
    // Flood-fill engine for sealed-room detection; cell-set building and leak gating live in CleanRoomCellSets.
    public static class CleanRoomDetector
    {
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
            CleanRoomCellSets.BuildCellSets(world, out var boundaryCells, out var occupiedCells);

            var rooms = new List<CleanRoom>();
            visitedCellCount = 0;
            if (boundaryCells.Count == 0) return rooms;

            // 境界セルの隣接セルを種に flood-fill を試みる。
            // Try flood-fill from each neighbor of every boundary cell as a seed.
            var seeds = new List<Vector3Int>();
            foreach (var boundaryCell in boundaryCells)
            foreach (var seed in CleanRoomCellSets.SixNeighbors(boundaryCell))
                seeds.Add(seed);

            return DetectFromSeeds(seeds, boundaryCells, occupiedCells, 0, out visitedCellCount);
        }

        // 与えた種集合から局所的に密閉部屋を検出する。RebuildAll と差分更新の共通エンジン。
        // 通過セルなら自身を、境界セルなら通過する6近傍を起点に flood-fill する。
        // Detect sealed rooms locally from the given seeds; shared engine for full and incremental scans.
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
                foreach (var n in CleanRoomCellSets.SixNeighbors(seed))
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
                    foreach (var n in CleanRoomCellSets.SixNeighbors(cur))
                    {
                        if (!boundaryCells.Contains(n)) continue;
                        if (isEmpty) surface++;
                        if (!bboxInit) { bMin = n; bMax = n; bboxInit = true; }
                        else { bMin = Vector3Int.Min(bMin, n); bMax = Vector3Int.Max(bMax, n); }
                    }

                    // 上限超過、または触れた壁AABB体積を大きく超える膨張はリーク。
                    // Cap overflow, or growth well past the touched-wall AABB volume, means a leak.
                    if (cellSet.Count > CleanRoomCellSets.MaxRoomVolume) { leaked = true; break; }
                    if (cellSet.Count > CleanRoomCellSets.LeakVisitedLimit(bboxInit, bMin, bMax)) { leaked = true; break; }

                    foreach (var n in CleanRoomCellSets.SixNeighbors(cur))
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
    }
}
