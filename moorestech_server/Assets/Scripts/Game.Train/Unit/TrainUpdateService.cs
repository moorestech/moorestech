using Game.Train.Unit.TickSynchronization;
using System;
using System.Collections.Generic;
using Core.Update;
using Game.Train.Diagram;
using Game.Train.RailGraph;
using UniRx;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Game.Train.Unit
{
    public class TrainUpdateService
    {
        private readonly TrainDiagramManager _diagramManager;
        private readonly IRailGraphDatastore _railGraphDatastore;
        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;
        private readonly TrainCarRidingManualCommandResolver _trainCarRidingManualCommandResolver;

        // Trainはサーバーのゲームtickに同期して進める
        // Train tick is aligned with the server game tick interval.
        private const double TickSeconds = GameUpdater.SecondsPerTick;
        public const double HashBroadcastIntervalSeconds = TickSeconds;
        private static readonly uint TrainUnitHashBroadcastIntervalTicks = Math.Max(4u, (uint)Math.Ceiling(HashBroadcastIntervalSeconds / TickSeconds));

        private readonly Subject<TrainHashStateEventData> _onHashEvent = new();
        private readonly Subject<(uint, IReadOnlyList<TrainTickDiffData>)> _onPreSimulationDiffEvent = new();
        private bool _trainAutoRunDebugEnabled;

        // 駆動はMasterTickUpdaterの固定順序がUpdateTrainsを呼ぶ（購読による暗黙順序を持たない）
        // Driven by MasterTickUpdater's fixed order calling UpdateTrains; no implicit subscription ordering
        public TrainUpdateService(
            TrainDiagramManager diagramManager,
            IRailGraphDatastore railGraphDatastore,
            ITrainUnitLookupDatastore trainUnitLookupDatastore,
            TrainCarRidingManualCommandResolver trainCarRidingManualCommandResolver)
        {
            _diagramManager = diagramManager;
            _railGraphDatastore = railGraphDatastore;
            _trainUnitLookupDatastore = trainUnitLookupDatastore;
            _trainCarRidingManualCommandResolver = trainCarRidingManualCommandResolver;
        }

        public IObservable<TrainHashStateEventData> OnHashEvent => _onHashEvent;
        public IObservable<(uint, IReadOnlyList<TrainTickDiffData>)> OnPreSimulationDiffEvent => _onPreSimulationDiffEvent;
        public bool IsTrainAutoRunDebugEnabled() => _trainAutoRunDebugEnabled;

        public void PublishCurrentTickHash(uint tick)
        {
            // hash計算タイミングはtrain側で管理し、間引き時はdummyを送る。
            // Keep train-specific hash cadence and dummy values here.
            _onHashEvent.OnNext(BuildHashStateEventData(tick));

            #region Internal
            TrainHashStateEventData BuildHashStateEventData(uint hashTick)
            {
                if (hashTick % TrainUnitHashBroadcastIntervalTicks != 0)
                {
                    return new TrainHashStateEventData(hashTick, uint.MaxValue, uint.MaxValue);
                }

                var bundles = new List<TrainUnitSnapshotBundle>();
                foreach (var train in _trainUnitLookupDatastore.GetRegisteredTrains())
                {
                    bundles.Add(TrainUnitSnapshotFactory.CreateSnapshot(train));
                }
                var unitsHash = TrainUnitSnapshotHashCalculator.Compute(bundles);
                var railGraphHash = _railGraphDatastore.GetConnectNodesHash();
                return new TrainHashStateEventData(hashTick, unitsHash, railGraphHash);
            }
            #endregion
        }

        public void UpdateTrains(uint tick)
        {
            // 乗車入力をtickごとに一括集計し、各TrainUnitへ適用する。
            // Aggregate riding inputs once per tick and apply them to each TrainUnit.
            var manualCommands = _trainCarRidingManualCommandResolver.ResolveAll(tick);
            foreach (var trainUnit in _trainUnitLookupDatastore.GetRegisteredTrains())
            {
                var manualCommand = manualCommands.TryGetValue(trainUnit, out var command) ? command : TrainUnitManualCommand.Default;
                trainUnit.Update(manualCommand);
            }

            NotifyPreSimulationDiff(tick);

            //↓これ以降にクライアントからの操作コマンド系適応がはいる、hashmismatchなどによるブロードキャストもはいる
            // Client command application and hash-mismatch broadcasting continue after this point.
            //snapshot,生成イベント系
            // Snapshot generation and creation events also continue after this point.
            return;

            #region Internal
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

        // TODO デバッグトグルスイッチ関連なので最終的に消すのを忘れずに
        // TODO remove this once the debug toggle switch flow is gone.
        private const string TrainAutoRunOnArgument = "on";
        private const string TrainAutoRunOffArgument = "off";

        // デバッグ用の自動運転切替
        // Toggle auto-run for debugging
        public void TurnOnorOffTrainAutoRun(IReadOnlyList<string> commandParts)
        {
            var mode = commandParts[1];
            if (string.Equals(mode, TrainAutoRunOnArgument, StringComparison.OrdinalIgnoreCase))
            {
                _trainAutoRunDebugEnabled = true;
                UnityEngine.Debug.Log("トグルスイッチ: Turning on auto-run for all trains.");
                AutoDiagramNodeAdditionExample();
                
                foreach (var train in _trainUnitLookupDatastore.GetRegisteredTrains())
                {
                    train.TurnOnAutoRun();
                }
            }

            if (string.Equals(mode, TrainAutoRunOffArgument, StringComparison.OrdinalIgnoreCase))
            {
                _trainAutoRunDebugEnabled = false;
                UnityEngine.Debug.Log("トグルスイッチ: Turning off auto-run for all trains.");
                foreach (var train in _trainUnitLookupDatastore.GetRegisteredTrains())
                {
                    train.TurnOffAutoRun();
                }
            }

            // on/off以外が来た場合はなにもしない
            // Ignore unsupported arguments.
            return;

            #region Internal

            // トグルスイッチを切り替えたときに全列車・全ダイアグラムを更新する。
            // Refresh every train and diagram when the toggle switch changes.
            // 既に存在する駅のfront exitノードを全てのダイアグラムに追加するだけ。
            // This currently just appends existing station front-exit nodes to every diagram.
            void AutoDiagramNodeAdditionExample()
            {
                // 自動運転の対象駅ノードを抽出する
                // Collect station nodes for auto-run
                var railNodes = _railGraphDatastore.GetRailNodes();
                var stationNodes = new List<RailNode>();
                for (int i = 0; i < railNodes.Count; i++)
                {
                    if (railNodes[i] != null)
                    {
                        // 蒸気機関車駅のBack側Exitノードだけをデバッグ自動運転に登録する
                        // Register only train station back-side exit nodes for debug auto-run.
                        if (IsDebugAutoRunStationNode(railNodes[i]))
                        {
                            stationNodes.Add(railNodes[i]);
                        }
                    }
                }
                _diagramManager.ResetAndNotifyNodeAddition(stationNodes);
            }

            bool IsDebugAutoRunStationNode(RailNode railNode)
            {
                if (railNode.StationRef.NodeSide != StationNodeSide.Back) return false;
                if (railNode.StationRef.NodeRole != StationNodeRole.Exit) return false;
                return railNode.StationRef.StationBlock?.BlockMasterElement.BlockType == BlockTypeConst.TrainStation;
            }

            #endregion
        }

    }
}
