using System;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
using UniRx;
using UnityEngine;

namespace Game.CleanRoom
{
    /// <summary>
    ///     ブロック増減を購読し、tick毎の差分再検出で部屋一覧を保持する
    ///     Subscribes to block changes and maintains rooms via per-tick incremental detection
    /// </summary>
    public class CleanRoomDatastore
    {
        public const int DirtyCellBudgetPerTick = 8192;

        public IReadOnlyList<CleanRoom> Rooms => _detectionService.Rooms;

        private readonly CleanRoomDetectionService _detectionService;
        private readonly IWorldBlockDatastore _world;
        private readonly List<CleanRoomThresholdRow> _thresholdRows;
        private readonly List<IDisposable> _subscriptions = new();

        public CleanRoomDatastore(IWorldBlockDatastore worldBlockDatastore)
        {
            _world = worldBlockDatastore;
            _detectionService = new CleanRoomDetectionService(worldBlockDatastore, DirtyCellBudgetPerTick);
            _thresholdRows = BuildThresholdRows();

            // tick毎に「①差分再検出 ②純度シミュレーション」の順で処理する
            // Each tick runs incremental re-detection first, then the purity simulation
            _subscriptions.Add(GameUpdater.UpdateObservable.Subscribe(_ =>
            {
                _detectionService.ProcessDirtySeeds();
                UpdatePurity();
            }));
            _subscriptions.Add(ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(properties => _detectionService.OnBlockChanged(properties.BlockData)));
            _subscriptions.Add(ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(properties => _detectionService.OnBlockChanged(properties.BlockData)));
        }

        public bool TryGetCleanRoomAt(Vector3Int cell, out CleanRoom room)
        {
            foreach (var candidate in _detectionService.Rooms)
            {
                if (!candidate.Contains(cell)) continue;
                room = candidate;
                return true;
            }

            room = null;
            return false;
        }

        public bool TryGetCleanRoom(IBlock block, out CleanRoom room)
        {
            room = null;
            var minPos = block.BlockPositionInfo.MinPos;
            var maxPos = block.BlockPositionInfo.MaxPos;

            // 全占有セルが同一部屋に属するときのみ成功とする
            // Succeed only when every occupied cell belongs to the same room
            for (var x = minPos.x; x <= maxPos.x; x++)
            for (var y = minPos.y; y <= maxPos.y; y++)
            for (var z = minPos.z; z <= maxPos.z; z++)
            {
                if (!TryGetCleanRoomAt(new Vector3Int(x, y, z), out var cellRoom) || (room != null && room != cellRoom))
                {
                    room = null;
                    return false;
                }

                room = cellRoom;
            }

            return room != null;
        }

        public void RebuildAll()
        {
            _detectionService.RebuildAll();
        }

        private void UpdatePurity()
        {
            // 閾値マスタが空なら純度シミュレーション全体を無効化する
            // With no threshold master, the whole purity simulation is disabled
            if (_thresholdRows.Count == 0) return;

            foreach (var room in _detectionService.Rooms)
            {
                // A_total算出 → 清浄機収集 → 積分 → 摩耗配分 → 閾値行更新の順に処理する
                // Per room: compute A_total, collect filters, integrate, distribute wear, update row
                var aTotal = CleanRoomPollution.ComputeATotal(room, _world);
                var filters = CollectAirFilters(room);
                var removalVolume = 0.0;
                foreach (var filter in filters) removalVolume += filter.RemovalVolumePerSecond;

                IntegrateAndDistributeWear(room, aTotal, filters, removalVolume);
                UpdateThresholdRow(room, removalVolume);
            }

            #region Internal

            List<ICleanRoomAirFilter> CollectAirFilters(CleanRoom room)
            {
                // 部屋セルに重なるブロックの清浄機を BlockInstanceId で重複排除して集める
                // Collect air filters on room cells, deduped by BlockInstanceId
                var filters = new List<ICleanRoomAirFilter>();
                var visited = new HashSet<BlockInstanceId>();
                foreach (var cell in room.Cells)
                {
                    if (!_world.TryGetBlock(cell, out var block)) continue;
                    if (!visited.Add(block.BlockInstanceId)) continue;
                    if (block.TryGetComponent<ICleanRoomAirFilter>(out var filter)) filters.Add(filter);
                }

                return filters;
            }

            void IntegrateAndDistributeWear(CleanRoom room, double aTotal, List<ICleanRoomAirFilter> filters, double removalVolume)
            {
                var oldImpurity = room.ImpurityCount;
                var deltaSeconds = GameUpdater.SecondsPerTick;
                room.SetImpurity(CleanRoomPurityLogic.IntegrateTick(oldImpurity, room.Volume, aTotal, removalVolume, deltaSeconds));

                // 今tickの除去量を各清浄機へ除去能力比で摩耗として配分する
                // Distribute this tick's removed amount to filters proportionally to capacity
                if (removalVolume <= 0 || room.Volume <= 0) return;
                var removed = Math.Min(oldImpurity, removalVolume * (oldImpurity / room.Volume) * deltaSeconds);
                if (removed <= 0) return;
                foreach (var filter in filters) filter.ApplyRemovedImpurity(removed * filter.RemovalVolumePerSecond / removalVolume);
            }

            void UpdateThresholdRow(CleanRoom room, double removalVolume)
            {
                // 濃度 N/V と換気率 nq/V から毎tick閾値行を更新する
                // Update the threshold row each tick from concentration N/V and ACH nq/V
                var concentration = room.ImpurityCount / room.Volume;
                var airChangeRate = removalVolume / room.Volume;
                room.SetThresholdIndex(CleanRoomPurityLogic.DecideThresholdIndex(room.ThresholdIndex, concentration, airChangeRate, _thresholdRows));
            }

            #endregion
        }

        private static List<CleanRoomThresholdRow> BuildThresholdRows()
        {
            // マスタの閾値行をロジック入力へ初期化時に1回だけ変換する
            // Convert master threshold rows into logic inputs once at initialization
            var rows = new List<CleanRoomThresholdRow>();
            foreach (var threshold in MasterHolder.CleanRoomMaster.Thresholds)
                rows.Add(new CleanRoomThresholdRow(threshold.MaxConcentration, threshold.RequiredAirChangeRate));
            return rows;
        }

        public void Destroy()
        {
            foreach (var subscription in _subscriptions) subscription.Dispose();
            _subscriptions.Clear();
        }
    }
}
