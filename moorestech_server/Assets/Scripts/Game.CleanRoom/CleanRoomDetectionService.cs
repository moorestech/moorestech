using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.CleanRoom
{
    /// <summary>
    ///     変更単位のシードバッチを予算内で局所再検出し部屋リストを差分更新する
    ///     Incrementally re-detects rooms from per-change seed batches within a per-tick budget
    /// </summary>
    public class CleanRoomDetectionService
    {
        public IReadOnlyList<CleanRoom> Rooms => _rooms;

        private readonly List<CleanRoom> _rooms = new();
        private readonly Queue<List<Vector3Int>> _pendingBatches = new();
        private readonly IWorldBlockDatastore _world;
        private readonly int _dirtyCellBudgetPerTick;
        private int _nextRoomId;

        public CleanRoomDetectionService(IWorldBlockDatastore world, int dirtyCellBudgetPerTick)
        {
            _world = world;
            _dirtyCellBudgetPerTick = dirtyCellBudgetPerTick;
        }

        public void OnBlockChanged(WorldBlockData blockData)
        {
            var isBoundary = blockData.Block.ComponentManager.ExistsComponent<ICleanRoomBoundaryComponent>();
            var minPos = blockData.BlockPositionInfo.MinPos;
            var maxPos = blockData.BlockPositionInfo.MaxPos;

            // この変更の全シードをバッチ内で重複排除しつつ1つのアトミックなバッチへ集約する
            // Collect all of this change's seeds into one atomic batch, deduped within the batch
            var seedSet = new HashSet<Vector3Int>();
            var batch = new List<Vector3Int>();
            for (var x = minPos.x; x <= maxPos.x; x++)
            for (var y = minPos.y; y <= maxPos.y; y++)
            for (var z = minPos.z; z <= maxPos.z; z++)
            {
                var cell = new Vector3Int(x, y, z);
                if (isBoundary)
                {
                    AddSeed(cell);
                    foreach (var neighbor in CleanRoomCellSets.SixNeighbors(cell)) AddSeed(neighbor);
                }
                else if (IsInsideAnyRoom(cell))
                {
                    AddSeed(cell);
                }
            }

            // シードを生まない変更（部屋外の非境界ブロック等）はバッチ化しない
            // A change yielding no seeds (e.g. a non-boundary block outside rooms) enqueues nothing
            if (batch.Count > 0) _pendingBatches.Enqueue(batch);

            #region Internal

            void AddSeed(Vector3Int cell)
            {
                if (seedSet.Add(cell)) batch.Add(cell);
            }

            #endregion
        }

        public void ProcessDirtySeeds()
        {
            if (_pendingBatches.Count == 0) return;

            // tick冒頭で1回だけセル集合を構築し全バッチで共有する
            // Build the cell sets once at tick start and share them across all batches
            CleanRoomCellSets.BuildCellSets(_world, out var boundaryCells, out var occupiedCells);

            var visitedTotal = 0;
            var processedBatchCount = 0;

            // バッチ単位で予算内を消化する。前進保証のため最低1バッチは丸ごと処理する
            // Drain whole batches within budget; always process at least one full batch for progress
            while (_pendingBatches.Count > 0 && (processedBatchCount == 0 || visitedTotal < _dirtyCellBudgetPerTick))
            {
                visitedTotal += ProcessBatch(_pendingBatches.Dequeue());
                processedBatchCount++;
            }

            #region Internal

            int ProcessBatch(List<Vector3Int> seeds)
            {
                // バッチ全シードを1回のflood-fillで検出し訪問集合を共有する（リーク再fillも排除）
                // Detect the whole batch in a single flood-fill sharing one visited set (dedups leak re-fills)
                var newRooms = CleanRoomDetector.DetectFromSeeds(seeds, boundaryCells, occupiedCells, _nextRoomId, out var visitedCellCount);
                _nextRoomId += newRooms.Count;

                // このバッチで差し替わる旧部屋を退避し、旧・新が同一スコープで共存するようにする
                // Move old rooms replaced by this batch aside so old and new coexist in one scope
                var oldRooms = TakeAffectedOldRooms(seeds, newRooms);
                CommitBatch(newRooms, oldRooms);
                return visitedCellCount;
            }

            List<CleanRoom> TakeAffectedOldRooms(List<Vector3Int> seeds, List<CleanRoom> newRooms)
            {
                var oldRooms = new List<CleanRoom>();
                for (var i = _rooms.Count - 1; i >= 0; i--)
                {
                    if (!IsAffected(_rooms[i])) continue;
                    oldRooms.Add(_rooms[i]);
                    _rooms.RemoveAt(i);
                }

                return oldRooms;

                bool IsAffected(CleanRoom room)
                {
                    // 探索に関与する非境界シードのみで判定する。健在な壁セルは隣接部屋を巻き込まない
                    // Judge only by non-boundary seeds; an intact wall cell must not drag its neighbor room in
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
                // 旧部屋と完全一致した新部屋は旧インスタンスをそのまま維持する
                // A new room identical to an old one keeps the old instance untouched
                for (var newIndex = newRooms.Count - 1; newIndex >= 0; newIndex--)
                {
                    var identicalOldRoom = FindIdenticalRoom(newRooms[newIndex], oldRooms);
                    if (identicalOldRoom == null) continue;
                    _rooms.Add(identicalOldRoom);
                    oldRooms.Remove(identicalOldRoom);
                    newRooms.RemoveAt(newIndex);
                }

                // 残りの新部屋へ旧部屋の状態を引き継いで確定する。引き継がれない旧状態は消滅する
                // Carry old state into the remaining new rooms and commit; uncarried old state dies here
                CleanRoomCarryOver.Apply(newRooms, oldRooms);
                _rooms.AddRange(newRooms);
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

        public void RebuildAll()
        {
            // ロード時用の全再検出。既存状態と未処理バッチは全て破棄する
            // Full re-detection for load; discards all existing state and pending batches
            _pendingBatches.Clear();
            _rooms.Clear();
            _rooms.AddRange(CleanRoomDetector.DetectAllRooms(_world, out _));
            _nextRoomId = _rooms.Count;
        }

        private bool IsInsideAnyRoom(Vector3Int cell)
        {
            foreach (var room in _rooms)
                if (room.Contains(cell))
                    return true;
            return false;
        }
    }
}
