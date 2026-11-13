using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CleanRoom.Save;
using Game.CleanRoom.Util;
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
        private readonly CleanRoomMachineEffectService _machineEffectService = new();
        private readonly CleanRoomAirFilterServingService _airFilterServingService = new();
        private readonly IWorldBlockDatastore _world;
        private readonly List<double> _requiredAirChangeRates;
        private readonly List<IDisposable> _subscriptions = new();

        public CleanRoomDatastore(IWorldBlockDatastore worldBlockDatastore)
        {
            _world = worldBlockDatastore;
            _detectionService = new CleanRoomDetectionService(worldBlockDatastore, DirtyCellBudgetPerTick);
            _requiredAirChangeRates = BuildRequiredAirChangeRates();

            _subscriptions.Add(ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(properties =>
            {
                _detectionService.OnBlockChanged(properties.BlockData);
                _machineEffectService.OnBlockPlaced(properties.BlockData);
            }));
            _subscriptions.Add(ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(properties =>
            {
                _detectionService.OnBlockChanged(properties.BlockData);
                _machineEffectService.OnBlockRemoved(properties.BlockData);
            }));
        }

        // 本体更新後に毎tick呼ぶ。①再検出②クラス判定と清浄機の在室③効果反映
        // Called every tick after the body update: detection, class decision with purifier serving, then machine effect pushes
        public void Update()
        {
            _detectionService.ProcessDirtySeeds();
            UpdatePurity();
            _machineEffectService.PushEffects(TryGetCleanRoom);
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

            // 在室は保存しないので、ロード直後の最初のtickから稼働中と同じ在室で回るよう再検出した部屋からすぐ配る
            // Serving state is not saved, so push it from the rebuilt rooms at once to run the first tick after load as a live world would
            foreach (var room in _detectionService.Rooms) CollectAndMarkServing(room);
            _airFilterServingService.PushChanges();
        }

        // クリーンルーム保存復元は専用ヘルパーへ委譲する
        // Delegate clean-room save and restore to the dedicated helper
        public List<CleanRoomSaveData> GetSaveData()
        {
            return CleanRoomSaveRestore.BuildSaveData(_detectionService.Rooms);
        }

        public void Restore(List<CleanRoomSaveData> saveData)
        {
            CleanRoomSaveRestore.Restore(saveData, _detectionService.Rooms);
        }

        private void UpdatePurity()
        {
            foreach (var room in _detectionService.Rooms)
            {
                var filters = CollectAndMarkServing(room);

                // 閾値マスタが空ならクラス判定を無効化する
                // With no threshold master, the class decision is disabled
                if (_requiredAirChangeRates.Count == 0) continue;

                // 除去能力と空きセル数で閾値行を決める
                // Decide the threshold row each tick from the purifiers' total removal volume and the empty cell count
                var airChangeRate = CleanRoomAirFilterCollector.AirChangeRate(room, filters);
                room.SetThresholdIndex(CleanRoomPurityLogic.DecideThresholdIndex(room.ThresholdIndex, airChangeRate, _requiredAirChangeRates));
            }

            // 部屋への出入りがあった清浄機へだけ在室をプッシュする
            // Push serving state only to purifiers that entered or left a room
            _airFilterServingService.PushChanges();
        }

        // 部屋内の清浄機を在室として記録し、クラス判定にも使えるよう返す
        // Record the purifiers inside the room as serving it and return them for the class decision
        private List<CleanRoomAirFilterEntry> CollectAndMarkServing(CleanRoom room)
        {
            var filters = CleanRoomAirFilterCollector.Collect(room, _world);
            _airFilterServingService.MarkServing(filters);
            return filters;
        }

        private static List<double> BuildRequiredAirChangeRates()
        {
            // 必要換気回数を初期化時に取り出す
            // Extract only the required air change rates from the master threshold rows once at initialization
            var rates = new List<double>();
            foreach (var threshold in MasterHolder.CleanRoomMaster.Thresholds) rates.Add(threshold.RequiredAirChangeRate);
            return rates;
        }

        public void Destroy()
        {
            foreach (var subscription in _subscriptions) subscription.Dispose();
            _subscriptions.Clear();
        }
    }
}
