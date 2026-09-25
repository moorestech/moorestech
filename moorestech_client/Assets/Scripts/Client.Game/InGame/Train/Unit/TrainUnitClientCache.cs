using System.Collections.Generic;
using Client.Game.InGame.Train.RailGraph;
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
        private readonly Dictionary<TrainUnitInstanceId, ClientTrainUnit> _units = new();
        // 車両スナップショット索引
        // Index for train car snapshots
        private readonly TrainCarSnapshotIndex _carSnapshots = new();

        // 列車一覧の読み取り専用ビュー
        // Read-only view for external systems
        public IReadOnlyDictionary<TrainUnitInstanceId, ClientTrainUnit> Units => _units;

        public TrainUnitClientCache(RailGraphClientCache railGraphProvider)
        {
            // レールグラフプロバイダを保持する
            // Keep the rail graph provider reference
            _railGraphProvider = railGraphProvider;
        }

        // 初期スナップショットでキャッシュ全体を入れ替える
        // Replace the entire cache when a full snapshot arrives
        public void OverrideAll(IReadOnlyList<TrainUnitSnapshotBundle> snapshots)
        {
            _units.Clear();
            _carSnapshots.Clear();
            if (snapshots == null)
            {
                return;
            }

            for (var i = 0; i < snapshots.Count; i++)
            {
                var bundle = snapshots[i];
                if (bundle.Simulation.TrainUnitInstanceId == TrainUnitInstanceId.Empty)
                {
                    continue;
                }

                var unit = new ClientTrainUnit(bundle.Simulation.TrainUnitInstanceId, _railGraphProvider);
                unit.SnapshotUpdate(bundle.Simulation, bundle.RailPositionSnapshot);
                _units[bundle.Simulation.TrainUnitInstanceId] = unit;
                _carSnapshots.BuildCarIndexForUnit(unit);
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
        public ClientTrainUnit Upsert(TrainUnitSnapshotBundle snapshot)
        {
            var trainUnitInstanceId = snapshot.Simulation.TrainUnitInstanceId;
            if (!_units.TryGetValue(trainUnitInstanceId, out var unit))
            {
                unit = new ClientTrainUnit(trainUnitInstanceId, _railGraphProvider);
                _units[trainUnitInstanceId] = unit;
            }

            _carSnapshots.RemoveCarIndex(trainUnitInstanceId);
            unit.SnapshotUpdate(snapshot.Simulation, snapshot.RailPositionSnapshot);
            _carSnapshots.BuildCarIndexForUnit(unit);
            return unit;
        }

        // pre sim差分イベントを対象TrainUnitへ反映する
        // Apply a pre-simulation diff event to the target train.
        public bool ApplyPreSimulationDiff(TrainUnitInstanceId trainUnitInstanceId, int masconLevelDiff, bool isNowDockingSpeedZero, int approachingNodeId, bool isReversedThisTick, int manualBranchSelectionIndexDiff)
        {
            if (!_units.TryGetValue(trainUnitInstanceId, out var unit))
            {
                return false;
            }

            // reverse diff は車両順とオフセット索引を変えるため、適用後に index を組み直す
            // Rebuild indexes after reverse diff because car order and offsets change
            var didReverse = unit.ApplyPreSimulationDiff(masconLevelDiff, isNowDockingSpeedZero, approachingNodeId, isReversedThisTick, manualBranchSelectionIndexDiff);
            if (didReverse)
            {
                _carSnapshots.RemoveCarIndex(trainUnitInstanceId);
                _carSnapshots.BuildCarIndexForUnit(unit);
            }
            return true;
        }

        public bool Remove(TrainUnitInstanceId trainUnitInstanceId)
        {
            _carSnapshots.RemoveCarIndex(trainUnitInstanceId);
            return _units.Remove(trainUnitInstanceId);
        }

        // 車両スナップショット索引を取得する
        // Resolve a cached car snapshot entry
        public bool TryGetCarSnapshot(TrainCarInstanceId id, out ClientTrainUnit unit, out TrainCarSnapshot snapshot, out int frontOffset, out int rearOffset)
        {
            return _carSnapshots.TryGetCarSnapshot(id, out unit, out snapshot, out frontOffset, out rearOffset);
        }

        // 列車情報の取得を試みる
        // Try retrieving the train info
        public bool TryGet(TrainUnitInstanceId trainUnitInstanceId, out ClientTrainUnit unit)
        {
            return _units.TryGetValue(trainUnitInstanceId, out unit);
        }

        internal void CopyUnitsTo(List<ClientTrainUnit> buffer)
        {
            buffer.Clear();
            buffer.AddRange(_units.Values);
        }
    }
}
