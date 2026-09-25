using System;
using System.Collections.Generic;
using Core.Update;
using Game.Train.RailGraph;
using UniRx;

namespace Game.Train.Unit
{
    public class TrainUpdateService
    {
        private readonly IRailGraphDatastore _railGraphDatastore;
        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;
        private readonly TrainCarRidingManualCommandResolver _trainCarRidingManualCommandResolver;

        // Trainはサーバーのゲームtickに同期して進める
        // Train tick is aligned with the server game tick interval.
        private const double TickSeconds = GameUpdater.SecondsPerTick;
        public const double HashBroadcastIntervalSeconds = TickSeconds;
        private static readonly uint TrainUnitHashBroadcastIntervalTicks = Math.Max(4u, (uint)Math.Ceiling(HashBroadcastIntervalSeconds / TickSeconds));
        private uint _executedTick;
        private uint _tickSequenceId;

        private readonly Subject<HashStateEventData> _onHashEvent = new();
        private readonly Subject<(uint, IReadOnlyList<TrainTickDiffData>)> _onPreSimulationDiffEvent = new();

        // 駆動はMasterTickUpdaterの固定順序がUpdateTrainsを呼ぶ（購読による暗黙順序を持たない）
        // Driven by MasterTickUpdater's fixed order calling UpdateTrains; no implicit subscription ordering
        public TrainUpdateService(
            IRailGraphDatastore railGraphDatastore,
            ITrainUnitLookupDatastore trainUnitLookupDatastore,
            TrainCarRidingManualCommandResolver trainCarRidingManualCommandResolver)
        {
            _railGraphDatastore = railGraphDatastore;
            _trainUnitLookupDatastore = trainUnitLookupDatastore;
            _trainCarRidingManualCommandResolver = trainCarRidingManualCommandResolver;
        }

        public uint GetCurrentTick() => _executedTick;
        public uint NextTickSequenceId()
        {
            // train/railイベント順序を表す単調IDを採番する
            // Issue a monotonic id that represents train/rail event order.
            _tickSequenceId++;
            return _tickSequenceId;
        }
        public uint GetCurrentTickSequenceId() => _tickSequenceId;
        public IObservable<HashStateEventData> OnHashEvent => _onHashEvent;
        public IObservable<(uint, IReadOnlyList<TrainTickDiffData>)> OnPreSimulationDiffEvent => _onPreSimulationDiffEvent;

        public void UpdateTrains()
        {
            // hash計算タイミングはTrainUpdateService側で管理し、間引き時はdummyを送る
            // TrainUpdateService owns hash timing and emits dummy on skipped ticks.
            var hashState = BuildHashStateEventData(_executedTick);
            _onHashEvent.OnNext(hashState);

            _executedTick++;
            // tickが進んだら同tick内順序のカウンタを初期化する
            // Reset per-tick ordering counter when tick advances.
            _tickSequenceId = 0;

            // 乗車入力をtickごとに一括集計し、各TrainUnitへ適用する。
            // Aggregate riding inputs once per tick and apply them to each TrainUnit.
            var manualCommands = _trainCarRidingManualCommandResolver.ResolveAll(_executedTick);
            foreach (var trainUnit in _trainUnitLookupDatastore.GetRegisteredTrains())
            {
                var manualCommand = manualCommands.TryGetValue(trainUnit, out var command) ? command : TrainUnitManualCommand.Default;
                trainUnit.Update(manualCommand);
            }

            NotifyPreSimulationDiff(_executedTick);

            //↓これ以降にクライアントからの操作コマンド系適応がはいる、hashmismatchなどによるブロードキャストもはいる
            // Client command application and hash-mismatch broadcasting continue after this point.
            //snapshot,生成イベント系
            // Snapshot generation and creation events also continue after this point.
            return;

            #region Internal
            HashStateEventData BuildHashStateEventData(uint hashTick)
            {
                if (hashTick % TrainUnitHashBroadcastIntervalTicks != 0)
                {
                    return new HashStateEventData(hashTick, uint.MaxValue, uint.MaxValue);
                }

                var bundles = new List<TrainUnitSnapshotBundle>();
                foreach (var train in _trainUnitLookupDatastore.GetRegisteredTrains())
                {
                    bundles.Add(TrainUnitSnapshotFactory.CreateSnapshot(train));
                }
                var unitsHash = TrainUnitSnapshotHashCalculator.Compute(bundles);
                var railGraphHash = _railGraphDatastore.GetConnectNodesHash();
                return new HashStateEventData(hashTick, unitsHash, railGraphHash);
            }

            // 全TrainUnitの差分を集約し、差分があるユニットのみ通知する
            // Aggregate per-unit diffs and publish only changed units.
            void NotifyPreSimulationDiff(uint tick)
            {
                var diffs = new List<TrainTickDiffData>();
                foreach (var trainUnit in _trainUnitLookupDatastore.GetRegisteredTrains())
                {
                    var (masconLevelDiff, isNowDockingSpeedZero, approachingNodeIdDiff, isReversedThisTick, manualBranchSelectionIndexDiff) = trainUnit.GetTickDiff();
                    if (!HasDiff(masconLevelDiff, isNowDockingSpeedZero, approachingNodeIdDiff, isReversedThisTick, manualBranchSelectionIndexDiff))
                    {
                        continue;
                    }
                    diffs.Add(new TrainTickDiffData(trainUnit.TrainUnitInstanceId, masconLevelDiff, isNowDockingSpeedZero, approachingNodeIdDiff, isReversedThisTick, manualBranchSelectionIndexDiff));
                }
                // 差分0件でもsim実行トリガとして同tickイベントを送る。
                // Emit the same-tick event even when diffs are empty as a simulation trigger.
                _onPreSimulationDiffEvent.OnNext((tick, diffs));
                
                bool HasDiff(int masconLevelDiff, bool isNowDockingSpeedZero, int approachingNodeIdDiff, bool isReversedThisTick, int manualBranchSelectionIndexDiff)
                {
                    return masconLevelDiff != 0 || isNowDockingSpeedZero || approachingNodeIdDiff != -1 || isReversedThisTick || manualBranchSelectionIndexDiff != 0;
                }
            }
            #endregion
        }

        public void ResetTick()
        {
            _executedTick = 0;
            _tickSequenceId = 0;
        }

        public readonly struct TrainTickDiffData
        {
            public TrainUnitInstanceId TrainUnitInstanceId { get; }
            public int MasconLevelDiff { get; }
            public bool IsNowDockingSpeedZero { get; }
            public int ApproachingNodeIdDiff { get; }
            public bool IsReversedThisTick { get; }
            public int ManualBranchSelectionIndexDiff { get; }

            public TrainTickDiffData(TrainUnitInstanceId trainUnitInstanceId, int masconLevelDiff, bool isNowDockingSpeedZero, int approachingNodeIdDiff, bool isReversedThisTick, int manualBranchSelectionIndexDiff)
            {
                TrainUnitInstanceId = trainUnitInstanceId;
                MasconLevelDiff = masconLevelDiff;
                IsNowDockingSpeedZero = isNowDockingSpeedZero;
                ApproachingNodeIdDiff = approachingNodeIdDiff;
                IsReversedThisTick = isReversedThisTick;
                ManualBranchSelectionIndexDiff = manualBranchSelectionIndexDiff;
            }
        }

        public readonly struct HashStateEventData
        {
            public uint Tick { get; }
            public uint UnitsHash { get; }
            public uint RailGraphHash { get; }

            public HashStateEventData(uint tick, uint unitsHash, uint railGraphHash)
            {
                Tick = tick;
                UnitsHash = unitsHash;
                RailGraphHash = railGraphHash;
            }
        }
    }
}
