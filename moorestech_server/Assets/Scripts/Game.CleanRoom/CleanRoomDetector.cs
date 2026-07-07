using System.Collections.Generic;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.CleanRoom
{
    /// <summary>
    ///     境界セルに囲まれた密閉領域を flood-fill で検出する
    ///     Detects sealed regions enclosed by boundary cells via flood-fill
    /// </summary>
    public static class CleanRoomDetector
    {
        public static List<CleanRoom> DetectAllRooms(IWorldBlockDatastore world, out int visitedCellCount)
        {
            CleanRoomCellSets.BuildCellSets(world, out var boundaryCells, out var occupiedCells);

            // 全境界セルの6近傍のうち非境界セルを種にする
            // Seeds are the non-boundary six-neighbors of every boundary cell
            var seedSet = new HashSet<Vector3Int>();
            var seeds = new List<Vector3Int>();
            foreach (var boundaryCell in boundaryCells)
            foreach (var neighbor in CleanRoomCellSets.SixNeighbors(boundaryCell))
                if (!boundaryCells.Contains(neighbor) && seedSet.Add(neighbor))
                    seeds.Add(neighbor);

            return DetectFromSeeds(seeds, boundaryCells, occupiedCells, 0, out visitedCellCount);
        }

        public static List<CleanRoom> DetectFromSeeds(IReadOnlyList<Vector3Int> seeds,
            HashSet<Vector3Int> boundaryCells, HashSet<Vector3Int> occupiedCells,
            int firstRoomId, out int visitedCellCount)
        {
            var rooms = new List<CleanRoom>();
            var nextRoomId = firstRoomId;

            // 訪問集合を種間で共有し、同一連結域を二度探索しない
            // Share the visited set across seeds so a connected region is explored only once
            var visited = new HashSet<Vector3Int>();
            var leakedCells = new HashSet<Vector3Int>();

            foreach (var seed in seeds)
            {
                if (boundaryCells.Contains(seed) || visited.Contains(seed)) continue;

                var room = FillFrom(seed);
                if (room != null) rooms.Add(room);
            }

            visitedCellCount = visited.Count;
            return rooms;

            #region Internal

            CleanRoom FillFrom(Vector3Int seed)
            {
                var fillCells = new HashSet<Vector3Int>();
                var queue = new Queue<Vector3Int>();
                var isLeaked = false;

                // 触れた境界セルのAABB。密閉部屋の内部はこの外接箱の内側に必ず収まる
                // AABB of touched boundary cells; a sealed interior always fits inside this box
                var bboxInitialized = false;
                var bboxMin = Vector3Int.zero;
                var bboxMax = Vector3Int.zero;

                visited.Add(seed);
                fillCells.Add(seed);
                queue.Enqueue(seed);

                while (!isLeaked && queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    foreach (var neighbor in CleanRoomCellSets.SixNeighbors(current))
                    {
                        // 境界セルは通過せず、触れた壁としてAABBを成長させる
                        // Boundary cells are impassable; grow the AABB of touched walls
                        if (boundaryCells.Contains(neighbor))
                        {
                            GrowBbox(neighbor);
                            continue;
                        }

                        if (fillCells.Contains(neighbor)) continue;

                        // 既訪問セルはリーク済み連結域の断片のみ。接触したこのfillもリーク
                        // A visited cell here belongs to a leaked region; touching it leaks this fill too
                        if (visited.Contains(neighbor))
                        {
                            if (leakedCells.Contains(neighbor)) isLeaked = true;
                            continue;
                        }

                        // 占有セル（非境界ブロック）も通過して部屋のセルに含める
                        // Occupied cells (non-boundary blocks) are passable and belong to the room
                        visited.Add(neighbor);
                        fillCells.Add(neighbor);
                        queue.Enqueue(neighbor);

                        // 予算超過＝密閉ではあり得ないためリーク確定
                        // Exceeding the budget can never happen for a sealed room, so it is a leak
                        if (fillCells.Count > CleanRoomCellSets.LeakVisitedLimit(bboxInitialized, bboxMin, bboxMax))
                        {
                            isLeaked = true;
                            break;
                        }
                    }
                }

                if (isLeaked)
                {
                    leakedCells.UnionWith(fillCells);
                    return null;
                }

                return CreateRoom();

                void GrowBbox(Vector3Int boundaryCell)
                {
                    if (!bboxInitialized)
                    {
                        bboxInitialized = true;
                        bboxMin = boundaryCell;
                        bboxMax = boundaryCell;
                        return;
                    }

                    bboxMin = Vector3Int.Min(bboxMin, boundaryCell);
                    bboxMax = Vector3Int.Max(bboxMax, boundaryCell);
                }

                CleanRoom CreateRoom()
                {
                    // Volume は空セルのみ、SurfaceArea は空セルが境界に接する面のみ数える
                    // Count only empty cells for Volume and their boundary-touching faces for SurfaceArea
                    var volume = 0;
                    var surfaceArea = 0;
                    foreach (var cell in fillCells)
                    {
                        if (occupiedCells.Contains(cell)) continue;

                        volume++;
                        foreach (var neighbor in CleanRoomCellSets.SixNeighbors(cell))
                            if (boundaryCells.Contains(neighbor))
                                surfaceArea++;
                    }

                    return new CleanRoom(nextRoomId++, fillCells, volume, surfaceArea);
                }
            }

            #endregion
        }
    }
}
