using System;
using System.Collections.Generic;
using Client.Game.InGame.Train.RailGraph;
using Core.Master;
using Game.Train.RailPositions;
using Game.Train.Unit;

namespace Client.Game.InGame.Train.Unit
{
    // RailGraphのキャッシュと同じように列車の状態を保持する
    // Cache that mirrors every train unit similar to the RailGraph cache
    public sealed class TrainUnitClientCache
    {
        // ローカルで追跡する列車一覧
        // Internal dictionary holding every tracked train
        private readonly RailGraphClientCache _railGraphProvider;
        private readonly Dictionary<TrainInstanceId, ClientTrainUnit> _units = new();
        // 車両スナップショット索引
        // Index for train car snapshots
        private readonly Dictionary<TrainCarInstanceId, TrainCarCacheEntry> _carIndex = new();
        private readonly Dictionary<TrainInstanceId, List<TrainCarInstanceId>> _carIdsByTrain = new();

        // 列車一覧の読み取り専用ビュー
        // Read-only view for external systems
        public IReadOnlyDictionary<TrainInstanceId, ClientTrainUnit> Units => _units;

        public TrainUnitClientCache(RailGraphClientCache railGraphProvider)
        {
            // レールグラフプロバイダを保持する
            // Keep the rail graph provider reference
            _railGraphProvider = railGraphProvider;
        }

        // 初期スナップショットでキャッシュ全体を入れ替える
        // Replace the entire cache when a full snapshot arrives
        public void OverrideAll(IReadOnlyList<(TrainSimulationSnapshot simulation, RailPosition railPosition)> snapshots)
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
                var (simulation, railPosition) = snapshots[i];
                if (simulation.TrainInstanceId == TrainInstanceId.Empty)
                {
                    continue;
                }

                var unit = new ClientTrainUnit(simulation.TrainInstanceId, _railGraphProvider);
                unit.SnapshotUpdate(simulation, railPosition);
                _units[simulation.TrainInstanceId] = unit;
                BuildCarIndexForUnit(unit);
            }
        }

        // 現在のTrainUnit状態からハッシュを計算する
        // Compute a hash from the current train unit cache
        public uint ComputeCurrentHash()
        {
            var units = new List<(TrainSimulationSnapshot simulation, RailPosition railPosition)>(_units.Count);
            foreach (var unit in _units.Values)
            {
                var railPosition = unit.RailPosition;
                if (railPosition == null)
                {
                    continue;
                }

                units.Add((
                    new TrainSimulationSnapshot(
                        unit.TrainInstanceId,
                        unit.CurrentSpeed,
                        unit.AccumulatedDistance,
                        unit.MasconLevel,
                        unit.Cars),
                    railPosition));
            }

            return TrainUnitSnapshotHashCalculator.Compute(units);
        }

        // 単一列車の差分更新を適用
        // Apply a diff snapshot for a single train
        public ClientTrainUnit Upsert(TrainSimulationSnapshot simulation, RailPosition railPosition)
        {
            var trainInstanceId = simulation.TrainInstanceId;
            if (!_units.TryGetValue(trainInstanceId, out var unit))
            {
                unit = new ClientTrainUnit(trainInstanceId, _railGraphProvider);
                _units[trainInstanceId] = unit;
            }

            RemoveCarIndex(trainInstanceId);
            unit.SnapshotUpdate(simulation, railPosition);
            BuildCarIndexForUnit(unit);
            return unit;
        }

        // pre sim差分イベントを対象TrainUnitへ反映する
        // Apply a pre-simulation diff event to the target train.
        public bool ApplyPreSimulationDiff(TrainInstanceId trainInstanceId, int masconLevelDiff, bool isNowDockingSpeedZero, int approachingNodeId)
        {
            if (!_units.TryGetValue(trainInstanceId, out var unit))
            {
                return false;
            }
            
            unit.ApplyPreSimulationDiff(masconLevelDiff, isNowDockingSpeedZero, approachingNodeId);
            return true;
        }

        // キャッシュから列車を削除
        // Remove a train from the cache
        // 指定TrainCarをキャッシュから削除し、列車索引を再構築する
        // Remove the specified train car and rebuild train indexes.
        public bool RemoveTrainCar(TrainCarInstanceId trainCarInstanceId)
        {
            if (!_carIndex.TryGetValue(trainCarInstanceId, out var entry))
            {
                return false;
            }

            var unit = entry.Unit;
            if (unit == null)
            {
                return false;
            }
            if (!unit.RemoveCar(trainCarInstanceId))
            {
                return false;
            }

            RemoveCarIndex(unit.TrainInstanceId);
            if (unit.Cars.Count == 0)
            {
                _units.Remove(unit.TrainInstanceId);
                return true;
            }

            BuildCarIndexForUnit(unit);
            return true;
        }

        public bool Remove(TrainInstanceId trainInstanceId)
        {
            RemoveCarIndex(trainInstanceId);
            return _units.Remove(trainInstanceId);
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
        public bool TryGet(TrainInstanceId trainInstanceId, out ClientTrainUnit unit)
        {
            return _units.TryGetValue(trainInstanceId, out unit);
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

            _carIdsByTrain[unit.TrainInstanceId] = carIds;

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

        private void RemoveCarIndex(TrainInstanceId trainInstanceId)
        {
            // 列車に紐づく車両索引を削除する
            // Remove car index entries for the target train
            if (!_carIdsByTrain.TryGetValue(trainInstanceId, out var carIds)) return;
            for (var i = 0; i < carIds.Count; i++) _carIndex.Remove(carIds[i]);
            _carIdsByTrain.Remove(trainInstanceId);
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
