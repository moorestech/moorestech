using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.CleanRoom.Util;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.CleanRoom
{
    /// <summary>
    ///     変更単位の開始点を処理量の上限内で調べ、部屋一覧を差分更新する
    ///     Incrementally updates rooms from per-change seed batches within a work budget
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

            // 一つのブロック変更から得た開始点を、重複のない一つのまとまりにする
            // Collect the seeds from one block change into one deduplicated batch
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

            // 部屋の外にある非境界ブロックなど、関係しない変更は追加しない
            // Ignore unrelated changes such as non-boundary blocks outside every room
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
            ProcessDirtySeeds(false);
        }

        public void ProcessAllDirtySeeds()
        {
            ProcessDirtySeeds(true);
        }

        public void RebuildAll()
        {
            // ロード時はブロック配置から全体を検出し、未処理の変更を破棄する
            // Re-detect every room from loaded blocks and discard pending changes
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

        private void ProcessDirtySeeds(bool drainAll)
        {
            // 通常更新と保存直前で同じ差分処理を使い、処理量の制限だけを切り替える
            // Share one incremental algorithm between ticks and saves, changing only its work limit
            _nextRoomId = CleanRoomDirtyBatchProcessor.Process(
                _rooms,
                _pendingBatches,
                _world,
                _dirtyCellBudgetPerTick,
                _nextRoomId,
                drainAll);
        }
    }
}
