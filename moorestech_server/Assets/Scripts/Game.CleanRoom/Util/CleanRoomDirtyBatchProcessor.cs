using System.Collections.Generic;
using Game.CleanRoom.Util;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.CleanRoom.Util
{
    internal static class CleanRoomDirtyBatchProcessor
    {
        public static int Process(
            List<CleanRoom> rooms,
            Queue<List<Vector3Int>> pendingBatches,
            IWorldBlockDatastore world,
            int dirtyCellBudget,
            int nextRoomId,
            bool drainAll)
        {
            if (pendingBatches.Count == 0) return nextRoomId;

            // セル集合は一度だけ構築し、今回処理する全変更で共有する
            // Build the cell sets once and share them across every change processed now
            CleanRoomCellSets.BuildCellSets(world, out var boundaryCells, out var occupiedCells);
            var visitedTotal = 0;
            var processedBatchCount = 0;

            // 通常時は上限内の変更を進め、保存時は未処理の変更をすべて確定する
            // Respect the normal budget, while a save drains every pending change
            while (0 < pendingBatches.Count &&
                   (drainAll || processedBatchCount == 0 || visitedTotal < dirtyCellBudget))
            {
                visitedTotal += ProcessBatch(pendingBatches.Dequeue());
                processedBatchCount++;
            }

            return nextRoomId;

            #region Internal

            int ProcessBatch(List<Vector3Int> seeds)
            {
                // 同じ変更由来の開始点を一度の探索で調べ、重複した探索を避ける
                // Detect one change batch in a shared flood-fill to avoid repeated searches
                var newRooms = CleanRoomDetector.DetectFromSeeds(
                    seeds,
                    boundaryCells,
                    occupiedCells,
                    nextRoomId,
                    out var visitedCellCount);
                nextRoomId += newRooms.Count;

                // この変更に関係する旧部屋を退避してから新しい形へ状態を引き継ぐ
                // Take aside old rooms affected by this change before carrying state forward
                var oldRooms = TakeAffectedOldRooms(seeds, newRooms);
                CommitBatch(newRooms, oldRooms);
                return visitedCellCount;
            }

            List<CleanRoom> TakeAffectedOldRooms(List<Vector3Int> seeds, List<CleanRoom> newRooms)
            {
                var oldRooms = new List<CleanRoom>();
                for (var i = rooms.Count - 1; 0 <= i; i--)
                {
                    if (!IsAffected(rooms[i])) continue;
                    oldRooms.Add(rooms[i]);
                    rooms.RemoveAt(i);
                }

                return oldRooms;

                bool IsAffected(CleanRoom room)
                {
                    // 壁そのものは隣室を巻き込まず、壁以外の変更だけを近傍判定に使う
                    // Exclude boundary seeds from neighbor checks so intact walls do not pull in adjacent rooms
                    foreach (var seed in seeds)
                    {
                        if (boundaryCells.Contains(seed)) continue;
                        if (room.Contains(seed)) return true;
                        foreach (var neighbor in CleanRoomCellSets.SixNeighbors(seed))
                            if (room.Contains(neighbor))
                                return true;
                    }

                    foreach (var newRoom in newRooms)
                    foreach (var cell in newRoom.Cells)
                        if (room.Contains(cell))
                            return true;
                    return false;
                }
            }

            void CommitBatch(List<CleanRoom> newRooms, List<CleanRoom> oldRooms)
            {
                // 形が変わらない部屋は既存インスタンスを維持する
                // Keep the existing instance when room geometry is unchanged
                for (var newIndex = newRooms.Count - 1; 0 <= newIndex; newIndex--)
                {
                    var identicalOldRoom = FindIdenticalRoom(newRooms[newIndex], oldRooms);
                    if (identicalOldRoom == null) continue;
                    rooms.Add(identicalOldRoom);
                    oldRooms.Remove(identicalOldRoom);
                    newRooms.RemoveAt(newIndex);
                }

                // 形が変わった部屋へ不純物などの状態を引き継いで確定する
                // Carry state into rooms whose geometry changed and commit them
                CleanRoomCarryOver.Apply(newRooms, oldRooms);
                rooms.AddRange(newRooms);
            }

            CleanRoom FindIdenticalRoom(CleanRoom newRoom, List<CleanRoom> oldRooms)
            {
                foreach (var oldRoom in oldRooms)
                {
                    if (oldRoom.Volume != newRoom.Volume || oldRoom.SurfaceArea != newRoom.SurfaceArea) continue;
                    if (oldRoom.Cells.Count != newRoom.Cells.Count) continue;

                    var allContained = true;
                    foreach (var cell in newRoom.Cells)
                        if (!oldRoom.Contains(cell))
                        {
                            allContained = false;
                            break;
                        }

                    if (allContained) return oldRoom;
                }

                return null;
            }

            #endregion
        }
    }
}
