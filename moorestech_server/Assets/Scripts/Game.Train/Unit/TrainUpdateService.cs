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

        // Trainはサーバーのゲームtickに同期し、1tick = 1/20秒で進める
        // Train tick is aligned with the server game tick (1 tick = 1/20 second).
        private const int Interval = GameUpdater.TicksPerSecond;
        public const double HashBroadcastIntervalSeconds = 1d;
        private readonly List<TrainUnit> _trainUnits = new();
        private long _executedTick;

        private readonly Subject<long> _onHashEvent = new();
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
        // 列車生成イベントの購読口を返す
        // Provide the train unit creation event stream
        public IObservable<TrainUnitInitializationNotifier.TrainUnitCreatedData> GetTrainUnitCreatedEvent() => _trainUnitInitializationNotifier.TrainUnitInitializedEvent;
        public bool IsTrainAutoRunDebugEnabled() => _trainAutoRunDebugEnabled;

        private void UpdateTrains()
        {
            for (int i = 0; i < GameUpdater.CurrentTickCount; i++)
            {
                //TODO
                //ここに操作コマンド系
                //
                
                //HashVerifier用ブロードキャスト
                if (_executedTick % Interval == 0)
                {
                    _onHashEvent.OnNext(_executedTick);
                }
                
                //simulation
                foreach (var trainUnit in _trainUnits)
                {
                    trainUnit.Update();
                }
                
                //ここにdiagram限定コマンド系(サーバーがブロードキャスト)
                //
                
                //_executedTick++;
                _executedTick++;
            }
        }

        private void UpdateTrains1Tickmanually()
        {
            foreach (var trainUnit in _trainUnits)
            {
                trainUnit.Update();
            }
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
