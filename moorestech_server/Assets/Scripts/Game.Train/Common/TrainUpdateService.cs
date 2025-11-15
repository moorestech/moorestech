using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Core.Update;
using Game.Train.RailGraph;
using Game.Train.Train;
using UniRx;

namespace Game.Train.Common
{
    public class TrainUpdateService
    {
        private static TrainUpdateService _instance;
        public static TrainUpdateService Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new TrainUpdateService();
                return _instance;
            }
        }

        //マジックナンバー。trainはtick制。1tickで速度、位置、ドッキング状態等が決定的に動く。1tick=1/120秒
        private const double TickSeconds = 1d / 120d;
        private double _accumulatedSeconds;
        private readonly int _maxTicksPerFrame = 65535;
        private readonly List<TrainUnit> _trainUnits = new();

        public TrainUpdateService()
        {
            GameUpdater.UpdateObservable
                .Subscribe(_ => UpdateTrains());
        }

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
            _trainUnits.Add(trainUnit);
        } 
        public void UnregisterTrain(TrainUnit trainUnit) => _trainUnits.Remove(trainUnit);
        public IEnumerable<TrainUnit> GetRegisteredTrains() => _trainUnits.ToArray();
        public void ResetTrains()
        {
            _trainUnits.Clear();
            _accumulatedSeconds = 0d;
        }


        // TODO デバッグトグルスイッチ関連なので最終的に消すのを忘れずに
        private const string TrainAutoRunOnArgument = "on";
        private const string TrainAutoRunOffArgument = "off";
        public static void TurnOnorOffTrainAutoRun(IReadOnlyList<string> commandParts)
        {
            var mode = commandParts[1];
            if (string.Equals(mode, TrainAutoRunOnArgument, StringComparison.OrdinalIgnoreCase))
            {
                UnityEngine.Debug.Log("トグルスイッチ：Turning on auto-run for all trains.");
                foreach (var train in Instance.GetRegisteredTrains())
                {
                    AutoDiagramNodeAdditionExample();
                    train.TurnOnAutoRun();
                }
            }

            if (string.Equals(mode, TrainAutoRunOffArgument, StringComparison.OrdinalIgnoreCase))
            {
                UnityEngine.Debug.Log("トグルスイッチ：Turning off auto-run for all trains.");
                foreach (var train in Instance.GetRegisteredTrains())
                {
                    train.TurnOffAutoRun();
                }
            }
            // on/off以外が来た場合はなにもしない
            return;
        }
        // トグルスイッチを切り替えたときに全部の列車の全部のダイアグラムを更新する。既に存在する駅のfront exitノードを全部のダイアグラムに追加するだけ
        private static void AutoDiagramNodeAdditionExample()
        {
            //まずRailGraphDataStoreから private List<RailNode> railNodesをリフレクションで取得
            var railNodesField = typeof(RailGraphDatastore).GetField("railNodes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var instanceField = typeof(RailGraphDatastore).GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var railNodes = (List<RailNode>)railNodesField.GetValue(instanceField);
            var stationNodes = new List<RailNode>();
            for (int i = 0; i < railNodes.Count; i++) 
            {
                if (railNodes[i] != null) 
                {
                    //駅ノードならfront exitノードを全部のダイアグラムに追加
                    if ((railNodes[i].StationRef.NodeSide == StationNodeSide.Front) && (railNodes[i].StationRef.NodeRole == StationNodeRole.Exit))
                    {
                        stationNodes.Add(railNodes[i]);
                    }
                }
            }
            TrainDiagramManager.Instance.ResetAndNotifyNodeAddition(stationNodes);
        }

#if UNITY_INCLUDE_TESTS
        public void ResetTickAccumulator()
        {
            _accumulatedSeconds = 0d;
        }
#endif
    }
}
