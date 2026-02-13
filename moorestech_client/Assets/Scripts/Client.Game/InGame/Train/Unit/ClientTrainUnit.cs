using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.RailCalc;
using Game.Train.Unit;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Client.Game.InGame.Train.Unit
{
    // クライアント上で扱う最小限の列車データ
    // Minimal client-side representation of a train
    public sealed class ClientTrainUnit
    {
        private readonly IRailGraphProvider _railGraphProvider;
        private readonly IRailGraphTraversalProvider _railGraphTraversalProvider;
        private bool _isDockingStopPendingForTick;
        private IRailNode _simulationTargetNode;

        public Guid TrainId { get; }
        public double CurrentSpeed { get; set; }
        public double AccumulatedDistance { get; set; }
        public int MasconLevel { get; set; }

        private IReadOnlyList<TrainCarSnapshot> _cars;
        // 車両スナップショットを外部に公開する
        public IReadOnlyList<TrainCarSnapshot> Cars => _cars ?? Array.Empty<TrainCarSnapshot>();
        public RailPosition RailPosition { get; private set; }

        public ClientTrainUnit(Guid trainId, IRailGraphProvider railGraphProvider)
        {
            // レールグラフプロバイダを保持する
            // Keep the rail graph provider reference
            _railGraphProvider = railGraphProvider;
            _railGraphTraversalProvider = railGraphProvider as IRailGraphTraversalProvider;
            TrainId = trainId;
        }

        // スナップショットの内容で内部状態を更新
        // Update internal state by the received snapshot
        public void SnapshotUpdate(TrainSimulationSnapshot simulation, RailPositionSaveData railPosition)
        {
            CurrentSpeed = simulation.CurrentSpeed;
            AccumulatedDistance = simulation.AccumulatedDistance;
            MasconLevel = simulation.MasconLevel;
            RailPosition = RailPositionFactory.Restore(railPosition, _railGraphProvider);
            _cars = simulation.Cars ?? Array.Empty<TrainCarSnapshot>();
            _simulationTargetNode = RailPosition?.GetNodeApproaching();
        }

        // pre sim差分イベントを反映する
        // Apply pre-simulation diff values from the server.
        public void ApplyPreSimulationDiff(int masconLevelDiff, bool isNowDockingSpeedZero, int approachingNodeId)
        {
            MasconLevel += masconLevelDiff;
            if (approachingNodeId != -1)
            {
                _railGraphTraversalProvider.TryGetNode(approachingNodeId, out _simulationTargetNode);
            }
            if (isNowDockingSpeedZero)
            {
                // このtickのシミュレーション内でドッキング停止処理を実行する
                _isDockingStopPendingForTick = true;
            }
        }

        // 現在の状態からスナップショットバンドルを生成する
        public bool TryCreateSnapshotBundle(out TrainUnitSnapshotBundle bundle)
        {
            if (RailPosition == null)
            {
                bundle = default;
                return false;
            }

            var simulation = CreateSimulationSnapshot();
            var railPosition = RailPosition.CreateSaveSnapshot();
            bundle = new TrainUnitSnapshotBundle(simulation, railPosition);
            return true;

            #region Internal

            TrainSimulationSnapshot CreateSimulationSnapshot()
            {
                // クライアントの移動状態をスナップショットへ変換する
                // Convert client-side motion state into a simulation snapshot
                var carSnapshots = _cars ?? Array.Empty<TrainCarSnapshot>();
                return new TrainSimulationSnapshot(
                    TrainId,
                    CurrentSpeed,
                    AccumulatedDistance,
                    MasconLevel,
                    carSnapshots);
            }

            #endregion
        }

        // 1tickごとに呼ばれる。進んだ距離を返す
        // Called every tick and returns moved distance
        public int Update()
        {
            // サーバー通知済みMasconLevelで速度シミュレーションを進める
            // Simulate movement using the server-synchronized mascon level.
            var distanceToMove = SimulateMotionStep();
            return UpdateTrainByDistance(distanceToMove);

            #region Internal

            int SimulateMotionStep()
            {
                // 速度と距離のステップ計算
                var tractionForce = MasconLevel > 0 ? UpdateTractionForce(MasconLevel) : 0.0;
                var stepInput = new TrainMotionStepInput(CurrentSpeed, AccumulatedDistance, MasconLevel, tractionForce);
                var stepResult = TrainDistanceSimulator.Step(stepInput);
                CurrentSpeed = stepResult.NewSpeed;
                AccumulatedDistance = stepResult.NewAccumulatedDistance;
                return stepResult.DistanceToMove;
                // 加速力を計算する
                // Calculate traction force
                double UpdateTractionForce(int masconLevel)
                {
                    var localCars = _cars ?? Array.Empty<TrainCarSnapshot>();
                    if (localCars.Count == 0) return 0;
                    int totalWeight = 0;
                    int totalTraction = 0;
                    foreach (var car in localCars)
                    {
                        var (weight, traction) = GetWeightAndTraction(car);
                        totalWeight += weight;
                        totalTraction += traction;
                    }
                    if (totalWeight == 0) return 0;
                    return (double)totalTraction / totalWeight * masconLevel / TrainMotionParameters.MasconLevelMaximum;
                    (int, int) GetWeightAndTraction(TrainCarSnapshot trainCarSnapshot)
                    {
                        return (TrainMotionParameters.DEFAULT_WEIGHT + trainCarSnapshot.InventorySlotsCount * TrainMotionParameters.WEIGHT_PER_SLOT, trainCarSnapshot.IsFacingForward ? trainCarSnapshot.TractionForce * TrainMotionParameters.DEFAULT_TRACTION : 0);
                    }
                }
            }
            #endregion
        }

        // Updateの距離int版
        public int UpdateTrainByDistance(int distanceToMove)
        {
            int totalMoved = 0;
            int loopCount = 0;
            while (true)
            {
                int moveLength = RailPosition.MoveForward(distanceToMove);
                distanceToMove -= moveLength;
                totalMoved += moveLength;

                // 目標ノードに到達したら停止
                if (IsArrivedDestination())
                {
                    if (_isDockingStopPendingForTick)
                    {
                        CurrentSpeed = 0;
                        AccumulatedDistance = 0;
                        _isDockingStopPendingForTick = false;
                        break;
                    }
                    else
                    {
                        if (distanceToMove > 0)
                        {
                            Debug.LogWarning("サーバーから停止の差分が届いてない可能性があります。次のhash検証でmismatchになる可能性あり");
                            break;
                        }
                    }
                }

                if (distanceToMove == 0) break;

                var approaching = RailPosition.GetNodeApproaching();
                if (approaching == null)
                {
                    CurrentSpeed = 0;
                    Debug.LogWarning("クライアント側でRailPositionの解決に失敗");
                    break;
                }

                var (found, newPath) = TryFindPathToSimulationTarget(approaching);
                if (!found)
                {
                    break;
                }

                RailPosition.AddNodeToHead(newPath[1]);

                loopCount++;
                if (loopCount > 1000000)
                {
                    throw new InvalidOperationException("列車速度が無限に近いか、レール経路の無限ループを検知しました。");
                }
            }
            return totalMoved;

            #region Internal
            bool IsArrivedDestination()
            {
                // 目標ノードに0距離で到達したか判定する
                var node = RailPosition.GetNodeApproaching();
                if (node == null || _simulationTargetNode == null)
                {
                    return false;
                }
                return (node.NodeGuid == _simulationTargetNode.NodeGuid) && (RailPosition.GetDistanceToNextNode() == 0);
            }

            #endregion
        }
        
        // 現在の目標ノードに到達する経路を探索する
        // Find path toward the current target node
        public (bool, List<IRailNode>) TryFindPathToSimulationTarget(IRailNode approaching)
        {
            if (approaching == null || _simulationTargetNode == null)
            {
                return (false, null);
            }
            var path = _railGraphProvider.FindShortestPath(approaching, _simulationTargetNode);
            var newPath = path?.ToList();
            if (newPath == null || newPath.Count < 2)
            {
                return (false, null);
            }
            return (true, newPath);
        }
    }
}

