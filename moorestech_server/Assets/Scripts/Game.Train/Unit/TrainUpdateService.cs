using System;
using System.Collections.Generic;
using Core.Update;
using Game.Train.Diagram;
using Game.Train.RailGraph;
using UniRx;

namespace Game.Train.Unit
{
    public class TrainUpdateService
    {
        private readonly TrainDiagramManager _diagramManager;
        private readonly IRailGraphDatastore _railGraphDatastore;
        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;

        // Trainはサーバーのゲームtickに同期して進める
        // Train tick is aligned with the server game tick interval.
        private const double TickSeconds = GameUpdater.SecondsPerTick;
        public const double HashBroadcastIntervalSeconds = TickSeconds;
        private static readonly uint TrainUnitHashBroadcastIntervalTicks = Math.Max(4u, (uint)Math.Ceiling(HashBroadcastIntervalSeconds / TickSeconds));
        private uint _executedTick;
        private uint _tickSequenceId;

        private readonly Subject<HashStateEventData> _onHashEvent = new();
        private readonly Subject<(uint, IReadOnlyList<TrainTickDiffData>)> _onPreSimulationDiffEvent = new();
        private bool _trainAutoRunDebugEnabled;

        // 依存サービスを受け取り、更新ループに接続する
        // Bind to required services and subscribe to update loop
        public TrainUpdateService(TrainDiagramManager diagramManager, IRailGraphDatastore railGraphDatastore,ITrainUnitLookupDatastore trainUnitLookupDatastore)
        {
            _diagramManager = diagramManager;
            _railGraphDatastore = railGraphDatastore;
            _trainUnitLookupDatastore = trainUnitLookupDatastore;
            GameUpdater.UpdateObservable.Subscribe(_ => UpdateTrains());
        }

        public uint GetCurrentTick() => _executedTick;
        public uint NextTickSequenceId()
        {
            // train/railイベント順序を表す単調IDを採番する
            // Issue a monotonic id that represents train/rail event order.
            _tickSequenceId++;
            return _tickSequenceId;
        }
        public IObservable<HashStateEventData> OnHashEvent => _onHashEvent;
        public IObservable<(uint, IReadOnlyList<TrainTickDiffData>)> OnPreSimulationDiffEvent => _onPreSimulationDiffEvent;
        public bool IsTrainAutoRunDebugEnabled() => _trainAutoRunDebugEnabled;

        private void UpdateTrains()
        {
            // hash計算タイミングはTrainUpdateService側で管理し、間引き時はdummyを送る
            // TrainUpdateService owns hash timing and emits dummy on skipped ticks.
            var hashState = BuildHashStateEventData(_executedTick);
            _onHashEvent.OnNext(hashState);

            _executedTick++;
            // tickが進んだら同tick内順序のカウンタを初期化する
            // Reset per-tick ordering counter when tick advances.
            _tickSequenceId = 0;

            //simulation
            foreach (var trainUnit in _trainUnitLookupDatastore.GetRegisteredTrains())
            {
                trainUnit.Update();
            }

            NotifyPreSimulationDiff(_executedTick);

            //↓これ以降にクライアントからの操作コマンド系適応がはいる、hashmismatchなどによるブロードキャストもはいる
            //snapshot,生成イベント系
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
                    var (masconLevelDiff, isNowDockingSpeedZero, approachingNodeIdDiff) = trainUnit.GetTickDiff();
                    if (!HasDiff(masconLevelDiff, isNowDockingSpeedZero, approachingNodeIdDiff))
                    {
                        continue;
                    }
                    diffs.Add(new TrainTickDiffData(trainUnit.TrainInstanceId, masconLevelDiff, isNowDockingSpeedZero, approachingNodeIdDiff));
                }
                // 差分0件でもsim実行トリガとして同tickイベントを送る。
                // Emit the same-tick event even when diffs are empty as a simulation trigger.
                _onPreSimulationDiffEvent.OnNext((tick, diffs));
                
                bool HasDiff(int masconLevelDiff, bool isNowDockingSpeedZero, int approachingNodeIdDiff)
                {
                    return masconLevelDiff != 0 || isNowDockingSpeedZero || approachingNodeIdDiff != -1;
                }
            }
            #endregion
        }

        public void ResetTick()
        {
            _executedTick = 0;
            _tickSequenceId = 0;
        }

        // TODO デバッグトグルスイッチ関連なので最終的に消すのを忘れずに
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
            return;

            #region Internal

            // トグルスイッチを切り替えたときに全列車・全ダイアグラムを更新する。
            // 既に存在する駅のfront exitノードを全てのダイアグラムに追加するだけ。
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
                        // 駅ノードならfront exitノードを全てのダイアグラムに追加
                        if ((railNodes[i].StationRef.NodeSide == StationNodeSide.Back) && (railNodes[i].StationRef.NodeRole == StationNodeRole.Exit))
                        {
                            stationNodes.Add(railNodes[i]);
                        }
                    }
                }
                _diagramManager.ResetAndNotifyNodeAddition(stationNodes);
            }

            #endregion
        }

        public readonly struct TrainTickDiffData
        {
            public TrainInstanceId TrainInstanceId { get; }
            public int MasconLevelDiff { get; }
            public bool IsNowDockingSpeedZero { get; }
            public int ApproachingNodeIdDiff { get; }

            public TrainTickDiffData(TrainInstanceId trainInstanceId, int masconLevelDiff, bool isNowDockingSpeedZero, int approachingNodeIdDiff)
            {
                TrainInstanceId = trainInstanceId;
                MasconLevelDiff = masconLevelDiff;
                IsNowDockingSpeedZero = isNowDockingSpeedZero;
                ApproachingNodeIdDiff = approachingNodeIdDiff;
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
