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

        // Trainはサーバーのゲームtickに同期して進める
        // Train tick is aligned with the server game tick interval.
        private const double TickSeconds = 0.1d;
        public const double HashBroadcastIntervalSeconds = 0.1d;
        private static readonly long TrainUnitHashBroadcastIntervalTicks = Math.Max(1L, (long)Math.Ceiling(HashBroadcastIntervalSeconds / TickSeconds));
        private readonly List<TrainUnit> _trainUnits = new();
        private long _executedTick;

        private readonly Subject<long> _onHashEvent = new();
        private readonly Subject<TrainTickDiffBatch> _onPreSimulationDiffEvent = new();
        private readonly TrainUnitInitializationNotifier _trainUnitInitializationNotifier;
        private bool _trainAutoRunDebugEnabled;

        // 依存サービスを受け取り、更新ループに接続する
        // Bind to required services and subscribe to update loop
        public TrainUpdateService(TrainDiagramManager diagramManager, IRailGraphDatastore railGraphDatastore)
        {
            _diagramManager = diagramManager;
            _railGraphDatastore = railGraphDatastore;
            // 列車生成通知のハブを初期化する
            // Initialize the train unit creation notifier
            _trainUnitInitializationNotifier = new TrainUnitInitializationNotifier();
            GameUpdater.UpdateObservable.Subscribe(_ => UpdateTrains());
        }

        public long GetCurrentTick() => _executedTick;
        public IObservable<long> GetOnHashEvent() => _onHashEvent;
        public IObservable<TrainTickDiffBatch> GetOnPreSimulationDiffEvent() => _onPreSimulationDiffEvent;
        // 列車生成イベントの購読口を返す
        // Provide the train unit creation event stream
        public IObservable<TrainUnitInitializationNotifier.TrainUnitCreatedData> GetTrainUnitCreatedEvent() => _trainUnitInitializationNotifier.TrainUnitInitializedEvent;
        public bool IsTrainAutoRunDebugEnabled() => _trainAutoRunDebugEnabled;

        private void UpdateTrains()
        {
            //HashVerifier用ブロードキャスト
            _onHashEvent.OnNext(_executedTick);
            
            _executedTick++;
            
            //simulation
            foreach (var trainUnit in _trainUnits)
            {
                trainUnit.Update();
            }

            NotifyPreSimulationDiff(_executedTick);
            
            //↓これ以降にクライアントからの操作コマンド系適応がはいる、hashmismatchなどによるブロードキャストもはいる
            //snapshot,生成イベント系

            return;

            #region Internal

            // 全TrainUnitの差分を集約し、差分があるユニットのみ通知する
            // Aggregate per-unit diffs and publish only changed units.
            void NotifyPreSimulationDiff(long tick)
            {
                var diffs = new List<TrainTickDiffData>();
                foreach (var trainUnit in _trainUnits)
                {
                    var (masconLevelDiff, isNowDockingSpeedZero, approachingNodeIdDiff) = trainUnit.GetTickDiff();
                    if (!HasDiff(masconLevelDiff, isNowDockingSpeedZero, approachingNodeIdDiff))
                    {
                        continue;
                    }
                    diffs.Add(new TrainTickDiffData(trainUnit.TrainId, masconLevelDiff, isNowDockingSpeedZero, approachingNodeIdDiff));
                }

                if (diffs.Count == 0)
                {
                    return;
                }

                _onPreSimulationDiffEvent.OnNext(new TrainTickDiffBatch(tick, diffs));
            }

            bool HasDiff(int masconLevelDiff, bool isNowDockingSpeedZero, int approachingNodeIdDiff)
            {
                return masconLevelDiff != 0 || isNowDockingSpeedZero || approachingNodeIdDiff != 0;
            }

            #endregion
        }
        
        public void RegisterTrain(TrainUnit trainUnit)
        {
            // 列車を登録して生成通知を送る
            // Register train and emit creation notification
            _trainUnits.Add(trainUnit);
            _trainUnitInitializationNotifier.Notify(trainUnit);
        }

        public void RegisterTrainWithoutNotify(TrainUnit trainUnit)
        {
            // 列車を通知なしで登録する
            // Register train without notification
            _trainUnits.Add(trainUnit);
        }
        public void UnregisterTrain(TrainUnit trainUnit) => _trainUnits.Remove(trainUnit);
        public IEnumerable<TrainUnit> GetRegisteredTrains() => _trainUnits.ToArray();

        public void ResetTrains()
        {
            _trainUnits.Clear();
            _executedTick = 0;
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
                foreach (var train in GetRegisteredTrains())
                {
                    train.TurnOnAutoRun();
                }
            }

            if (string.Equals(mode, TrainAutoRunOffArgument, StringComparison.OrdinalIgnoreCase))
            {
                _trainAutoRunDebugEnabled = false;
                UnityEngine.Debug.Log("トグルスイッチ: Turning off auto-run for all trains.");
                foreach (var train in GetRegisteredTrains())
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

        public readonly struct TrainTickDiffBatch
        {
            public long Tick { get; }
            public IReadOnlyList<TrainTickDiffData> Diffs { get; }

            public TrainTickDiffBatch(long tick, IReadOnlyList<TrainTickDiffData> diffs)
            {
                Tick = tick;
                Diffs = diffs;
            }
        }

        public readonly struct TrainTickDiffData
        {
            public Guid TrainId { get; }
            public int MasconLevelDiff { get; }
            public bool IsNowDockingSpeedZero { get; }
            public int ApproachingNodeIdDiff { get; }

            public TrainTickDiffData(Guid trainId, int masconLevelDiff, bool isNowDockingSpeedZero, int approachingNodeIdDiff)
            {
                TrainId = trainId;
                MasconLevelDiff = masconLevelDiff;
                IsNowDockingSpeedZero = isNowDockingSpeedZero;
                ApproachingNodeIdDiff = approachingNodeIdDiff;
            }
        }
    }
}
