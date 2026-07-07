using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.CleanRoom
{
    /// <summary>
    ///     dirtyシードを予算内で局所再検出し部屋リストを差分更新する
    ///     Incrementally re-detects rooms from dirty seeds within a per-tick budget
    /// </summary>
    public class CleanRoomDetectionService
    {
        public IReadOnlyList<CleanRoom> Rooms => _rooms;

        private readonly List<CleanRoom> _rooms = new();
        private readonly HashSet<Vector3Int> _dirtySeeds = new();
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

            // 境界ブロックは占有セル+6近傍を、非境界は既存部屋に重なる占有セルのみシード化
            // Boundary blocks seed their cells plus neighbors; others seed only cells overlapping existing rooms
            for (var x = minPos.x; x <= maxPos.x; x++)
            for (var y = minPos.y; y <= maxPos.y; y++)
            for (var z = minPos.z; z <= maxPos.z; z++)
            {
                var cell = new Vector3Int(x, y, z);
                if (isBoundary)
                {
                    _dirtySeeds.Add(cell);
                    foreach (var neighbor in CleanRoomCellSets.SixNeighbors(cell)) _dirtySeeds.Add(neighbor);
                }
                else if (IsInsideAnyRoom(cell))
                {
                    _dirtySeeds.Add(cell);
                }
            }
        }

        public void ProcessDirtySeeds()
        {
            if (_dirtySeeds.Count == 0) return;

            // tick冒頭で1回だけセル集合を構築し全シードで共有する
            // Build the cell sets once at tick start and share them across all seeds
            CleanRoomCellSets.BuildCellSets(_world, out var boundaryCells, out var occupiedCells);

            var oldRoomsBatch = new List<CleanRoom>();
            var newRoomsBatch = new List<CleanRoom>();
            var visitedTotal = 0;
            var processedSeedCount = 0;

            // 予算内でシードを消化する。前進保証のため最低1シードは必ず処理する
            // Drain seeds within budget; always process at least one seed to guarantee progress
            while (_dirtySeeds.Count > 0 && (processedSeedCount == 0 || visitedTotal < _dirtyCellBudgetPerTick))
            {
                var seed = TakeSeed();
                visitedTotal += ProcessSeed(seed);
                processedSeedCount++;
            }

            CommitBatch();

            #region Internal

            Vector3Int TakeSeed()
            {
                var seed = Vector3Int.zero;
                foreach (var candidate in _dirtySeeds)
                {
                    seed = candidate;
                    break;
                }

                _dirtySeeds.Remove(seed);
                return seed;
            }

            int ProcessSeed(Vector3Int seed)
            {
                // 境界セル自身のシードは探索も部屋差し替えも起こさない（近傍シードが担当する）
                // A boundary-cell seed triggers no fill and no room replacement; its neighbor seeds handle it
                if (boundaryCells.Contains(seed)) return 0;

                // 同tickで検出済みの新部屋内のシードは再探索不要
                // Seeds already inside a room detected this tick need no re-fill
                foreach (var newRoom in newRoomsBatch)
                    if (newRoom.Contains(seed))
                        return 0;

                var detected = CleanRoomDetector.DetectFromSeeds(new[] { seed }, boundaryCells, occupiedCells, _nextRoomId, out var visitedCellCount);
                _nextRoomId += detected.Count;

                CollectAffectedOldRooms(seed, detected);
                newRoomsBatch.AddRange(detected);
                return visitedCellCount;
            }

            void CollectAffectedOldRooms(Vector3Int seed, List<CleanRoom> detected)
            {
                // シード近傍か新部屋セルに重なる既存部屋を差し替え対象として退避する
                // Move rooms touching the seed or overlapping new room cells into the batch
                for (var i = _rooms.Count - 1; i >= 0; i--)
                {
                    if (!IsAffected(_rooms[i])) continue;
                    oldRoomsBatch.Add(_rooms[i]);
                    _rooms.RemoveAt(i);
                }

                bool IsAffected(CleanRoom room)
                {
                    if (room.Contains(seed)) return true;
                    foreach (var neighbor in CleanRoomCellSets.SixNeighbors(seed))
                        if (room.Contains(neighbor))
                            return true;
                    foreach (var newRoom in detected)
                    foreach (var cell in newRoom.Cells)
                        if (room.Contains(cell))
                            return true;
                    return false;
                }
            }

            void CommitBatch()
            {
                // 旧部屋と完全一致した新部屋は旧インスタンスをそのまま維持する
                // A new room identical to an old one keeps the old instance untouched
                for (var newIndex = newRoomsBatch.Count - 1; newIndex >= 0; newIndex--)
                {
                    var identicalOldRoom = FindIdenticalRoom(newRoomsBatch[newIndex]);
                    if (identicalOldRoom == null) continue;
                    _rooms.Add(identicalOldRoom);
                    oldRoomsBatch.Remove(identicalOldRoom);
                    newRoomsBatch.RemoveAt(newIndex);
                }

                // 残りの新部屋へ旧部屋の状態を引き継いで確定する。引き継がれない旧状態は消滅する
                // Carry old state into the remaining new rooms and commit; uncarried old state dies here
                CleanRoomCarryOver.Apply(newRoomsBatch, oldRoomsBatch);
                _rooms.AddRange(newRoomsBatch);
            }

            CleanRoom FindIdenticalRoom(CleanRoom newRoom)
            {
                foreach (var oldRoom in oldRoomsBatch)
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
            // ロード時用の全再検出。既存状態と未処理シードは全て破棄する
            // Full re-detection for load; discards all existing state and pending seeds
            _dirtySeeds.Clear();
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
