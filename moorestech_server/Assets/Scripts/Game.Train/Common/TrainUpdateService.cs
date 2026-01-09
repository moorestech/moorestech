using System;
using System.Collections.Generic;
using Core.Update;
using Game.Train.RailGraph;
using Game.Train.Train;
using UniRx;

namespace Game.Train.Common
{
    public class TrainUpdateService
    {
        private readonly TrainDiagramManager _diagramManager;
        private readonly IRailGraphDatastore _railGraphDatastore;

        // マジックナンバー。trainはtick制で、tickで速度、位置、ドッキング状態等が決定的に動く。tick=1/60秒
        private const int interval = 60;
        private const double TickSeconds = 1d / interval;
        public const double HashBroadcastIntervalSeconds = 1d;
        private double _accumulatedSeconds;
        private readonly int _maxTicksPerFrame = 65535;
        private readonly List<TrainUnit> _trainUnits = new();
        private long _executedTick;

        private readonly Subject<long> _onHashEvent = new();
        private bool _trainAutoRunDebugEnabled;

        // 依存サービスを受け取り、更新ループに接続する
        // Bind to required services and subscribe to update loop
        public TrainUpdateService(TrainDiagramManager diagramManager, IRailGraphDatastore railGraphDatastore)
        {
            _diagramManager = diagramManager;
            _railGraphDatastore = railGraphDatastore;
            GameUpdater.UpdateObservable.Subscribe(_ => UpdateTrains());
        }

        public long GetCurrentTick() => _executedTick;
        public IObservable<long> GetOnHashEvent() => _onHashEvent;
        public bool IsTrainAutoRunDebugEnabled() => _trainAutoRunDebugEnabled;

        private void UpdateTrains()
        {
            _accumulatedSeconds += GameUpdater.UpdateSecondTime;

            var tickCount = Math.Min(_maxTicksPerFrame, (int)(_accumulatedSeconds / TickSeconds));
            if (tickCount == 0)
            {
                return;
            }

            _accumulatedSeconds -= tickCount * TickSeconds;

            for (var i = 0; i < tickCount; i++)
            {
                foreach (var trainUnit in _trainUnits)
                {
                    trainUnit.Update();
                }
                _executedTick++;
                if (_executedTick % interval == 0)
                {
                    _onHashEvent.OnNext(_executedTick);
                }

                // 外部スナップショットを毎tick記録する
                // Record external snapshots per tick
                //TrainTickSnapshotRecorder.RecordTickIfAvailable(_executedTick, _trainUnits);
            }
        }

        private void UpdateTrains1Tickmanually()
        {
            foreach (var trainUnit in _trainUnits)
            {
                trainUnit.Update();
            }
        }

        public void RegisterTrain(TrainUnit trainUnit) => _trainUnits.Add(trainUnit);
        public void UnregisterTrain(TrainUnit trainUnit) => _trainUnits.Remove(trainUnit);
        public IEnumerable<TrainUnit> GetRegisteredTrains() => _trainUnits.ToArray();

        public void ResetTrains()
        {
            _trainUnits.Clear();
            _accumulatedSeconds = 0d;
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
        }

        // トグルスイッチを切り替えたときに全列車・全ダイアグラムを更新する。
        // 既に存在する駅のfront exitノードを全てのダイアグラムに追加するだけ。
        private void AutoDiagramNodeAdditionExample()
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
    }
}
