using System;
using System.Collections.Generic;
using Client.Game.InGame.Train.RailGraph;
using Core.Master;
using Game.Train.Unit;
using Server.Util.MessagePack;

namespace Client.Game.InGame.Train.Unit
{
    // RailGraphのキャッシュと同じように列車の状態を保持する
    // Cache that mirrors every train unit similar to the RailGraph cache
    public sealed class TrainUnitClientCache
    {
        // ローカルで追跡する列車一覧
        // Internal dictionary holding every tracked train
        private readonly RailGraphClientCache _railGraphProvider;
        private readonly Dictionary<Guid, ClientTrainUnit> _units = new();
        // 車両スナップショット索引
        // Index for train car snapshots
        private readonly Dictionary<TrainCarInstanceId, TrainCarCacheEntry> _carIndex = new();
        private readonly Dictionary<Guid, List<TrainCarInstanceId>> _carIdsByTrain = new();

        // 列車一覧の読み取り専用ビュー
        // Read-only view for external systems
        public IReadOnlyDictionary<Guid, ClientTrainUnit> Units => _units;

        public TrainUnitClientCache(RailGraphClientCache railGraphProvider)
        {
            // レールグラフプロバイダを保持する
            // Keep the rail graph provider reference
            _railGraphProvider = railGraphProvider;
        }

        // 初期スナップショットでキャッシュ全体を入れ替える
        // Replace the entire cache when a full snapshot arrives
        public void OverrideAll(IReadOnlyList<TrainUnitSnapshotBundle> snapshots, long serverTick)
        {
            _units.Clear();
            _carIndex.Clear();
            _carIdsByTrain.Clear();
            if (snapshots == null)
            {
                return;
            }

            for (var i = 0; i < snapshots.Count; i++)
            {
                var bundle = snapshots[i];
                if (bundle.Simulation.TrainId == Guid.Empty)
                {
                    continue;
                }

                var unit = new ClientTrainUnit(bundle.Simulation.TrainId, _railGraphProvider);
                unit.SnapshotUpdate(bundle.Simulation, bundle.RailPositionSnapshot, serverTick);
                _units[bundle.Simulation.TrainId] = unit;
                BuildCarIndexForUnit(unit);
            }
        }

        // 現在のTrainUnit状態からハッシュを計算する
        // Compute a hash from the current train unit cache
        public uint ComputeCurrentHash()
        {
            var bundles = new List<TrainUnitSnapshotBundle>(_units.Count);
            foreach (var unit in _units.Values)
            {
                if (!unit.TryCreateSnapshotBundle(out var bundle))
                {
                    continue;
                }
                bundles.Add(bundle);
            }
            return TrainUnitSnapshotHashCalculator.Compute(bundles);
        }

        // 単一列車の差分更新を適用
        // Apply a diff snapshot for a single train
        public ClientTrainUnit Upsert(TrainUnitSnapshotBundle snapshot, long serverTick)
        {
            var trainId = snapshot.Simulation.TrainId;
            if (!_units.TryGetValue(trainId, out var unit))
            {
                unit = new ClientTrainUnit(trainId, _railGraphProvider);
                _units[trainId] = unit;
            }

            RemoveCarIndex(trainId);
            unit.SnapshotUpdate(snapshot.Simulation, snapshot.RailPositionSnapshot, serverTick);
            BuildCarIndexForUnit(unit);
            return unit;
        }

        // キャッシュから列車を削除
        // Remove a train from the cache
        public bool Remove(Guid trainId)
        {
            RemoveCarIndex(trainId);
            return _units.Remove(trainId);
        }

        // 車両スナップショット索引を取得する
        // Resolve a cached car snapshot entry
        public bool TryGetCarSnapshot(TrainCarInstanceId trainCarInstanceId, out ClientTrainUnit unit, out TrainCarSnapshot snapshot, out int frontOffset, out int rearOffset)
        {
            // 出力を初期化する
            // Initialize output values
            unit = null;
            snapshot = default;
            frontOffset = 0;
            rearOffset = 0;

            // 索引から対象車両を取得する
            // Lookup the target car from the index
            if (!_carIndex.TryGetValue(trainCarInstanceId, out var entry)) return false;
            unit = entry.Unit;
            snapshot = entry.Snapshot;
            frontOffset = entry.FrontOffset;
            rearOffset = entry.RearOffset;
            return true;
        }

        // 列車情報の取得を試みる
        // Try retrieving the train info
        public bool TryGet(Guid trainId, out ClientTrainUnit unit)
        {
            return _units.TryGetValue(trainId, out unit);
        }

        internal void CopyUnitsTo(List<ClientTrainUnit> buffer)
        {
            buffer.Clear();
            buffer.AddRange(_units.Values);
        }

        #region Internal

        private void BuildCarIndexForUnit(ClientTrainUnit unit)
        {
            // 車両スナップショットから索引を構築する
            // Build car index entries from snapshots
            var cars = unit.Cars;
            if (cars.Count == 0) return;

            var carIds = new List<TrainCarInstanceId>(cars.Count);
            var offsetFromHead = 0;
            for (var i = 0; i < cars.Count; i++)
            {
                // 車両長さを算出し前後オフセットを登録する
                // Resolve length and store front/rear offsets
                var carSnapshot = cars[i];
                var carLength = ResolveCarLength(carSnapshot);
                if (carLength <= 0) continue;
                var frontOffset = offsetFromHead;
                var rearOffset = offsetFromHead + carLength;
                offsetFromHead += carLength;
                _carIndex[carSnapshot.TrainCarInstanceId] = new TrainCarCacheEntry(unit, carSnapshot, frontOffset, rearOffset);
                carIds.Add(carSnapshot.TrainCarInstanceId);
            }

            _carIdsByTrain[unit.TrainId] = carIds;

            #region Internal

            int ResolveCarLength(TrainCarSnapshot snapshot)
            {
                // マスター情報から車両長さを解決する
                // Resolve car length from master data
                if (MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(snapshot.TrainCarMasterId, out var master) && master.Length > 0) return TrainLengthConverter.ToRailUnits(master.Length);
                return 0;
            }

            #endregion
        }

        private void RemoveCarIndex(Guid trainId)
        {
            // 列車に紐づく車両索引を削除する
            // Remove car index entries for the target train
            if (!_carIdsByTrain.TryGetValue(trainId, out var carIds)) return;
            for (var i = 0; i < carIds.Count; i++) _carIndex.Remove(carIds[i]);
            _carIdsByTrain.Remove(trainId);
        }

        private readonly struct TrainCarCacheEntry
        {
            public readonly ClientTrainUnit Unit;
            public readonly TrainCarSnapshot Snapshot;
            public readonly int FrontOffset;
            public readonly int RearOffset;

            public TrainCarCacheEntry(ClientTrainUnit unit, TrainCarSnapshot snapshot, int frontOffset, int rearOffset)
            {
                // 索引の内容を初期化する
                // Initialize entry values
                Unit = unit;
                Snapshot = snapshot;
                FrontOffset = frontOffset;
                RearOffset = rearOffset;
            }
        }

        #endregion
    }
}
