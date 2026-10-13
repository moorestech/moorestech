using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using UnityEngine;

namespace Client.Game.InGame.Train.Unit
{
    internal sealed class ClientTrainUnitMotion
    {
        private readonly IRailGraphProvider _railGraphProvider;
        private readonly IRailGraphTraversalProvider _railGraphTraversalProvider;
        private IRailNode _simulationTargetNode;
        private bool _isDockingStopPendingForTick;

        public ClientTrainUnitMotion(IRailGraphProvider railGraphProvider)
        {
            _railGraphProvider = railGraphProvider;
            _railGraphTraversalProvider = railGraphProvider as IRailGraphTraversalProvider;
        }

        public void ResetTarget(RailPosition railPosition)
        {
            _simulationTargetNode = railPosition?.GetNodeApproaching();
        }

        public void SetApproachingNode(int approachingNodeId)
        {
            _railGraphTraversalProvider.TryGetNode(approachingNodeId, out _simulationTargetNode);
        }

        public void QueueDockingStop()
        {
            _isDockingStopPendingForTick = true;
        }

        public TrainMotionStepResult SimulateStep(
            double currentSpeed,
            double accumulatedDistance,
            int masconLevel,
            IReadOnlyList<TrainCarSnapshot> cars)
        {
            // 車両重量と牽引力からこの tick の移動量を算出する
            // Calculate this tick's movement from car weight and traction
            var totalWeight = 0;
            var totalTraction = 0;
            foreach (var car in cars)
            {
                MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(car.TrainCarMasterId, out var trainElement);
                totalWeight += car.Weight;
                totalTraction += trainElement.TractionForce;
            }
            var input = new TrainMotionStepInput(currentSpeed, accumulatedDistance, masconLevel, totalTraction, totalWeight);
            return TrainDistanceSimulator.Step(input);
        }

        public int UpdateTrainByDistance(RailPosition railPosition, int distanceToMove, ref double currentSpeed, ref double accumulatedDistance)
        {
            var totalMoved = 0;
            var loopCount = 0;
            while (true)
            {
                var moveLength = railPosition.MoveForward(distanceToMove);
                distanceToMove -= moveLength;
                totalMoved += moveLength;

                // 目標ノード到着時はサーバー通知の停車を適用
                // Apply the server-notified stop on reaching the target node
                if (IsArrivedDestination())
                {
                    if (_isDockingStopPendingForTick)
                    {
                        currentSpeed = 0;
                        accumulatedDistance = 0;
                        _isDockingStopPendingForTick = false;
                        break;
                    }
                    if (0 < distanceToMove)
                    {
                        Debug.LogWarning("1st hashよりApplySnapshotTrainUnitのtickが前ならこれは想定内です。次のhash検証でmismatchになる可能性あり");
                        break;
                    }
                }

                if (distanceToMove == 0)
                {
                    break;
                }

                var approaching = railPosition.GetNodeApproaching();
                if (approaching == null)
                {
                    currentSpeed = 0;
                    Debug.LogWarning("クライアント側でRailPositionの解決に失敗");
                    break;
                }

                var (found, newPath) = TryFindPathToSimulationTarget(approaching);
                if (!found)
                {
                    Debug.LogWarning("クライアント側で分岐またぎの解決に失敗");
                    break;
                }
                railPosition.AddNodeToHead(newPath[1]);

                loopCount++;
                if (1000000 < loopCount)
                {
                    throw new InvalidOperationException("列車速度が無限に近いか、レール経路の無限ループを検知しました。");
                }
            }
            return totalMoved;

            #region Internal

            bool IsArrivedDestination()
            {
                // 目標ノードに距離0で到達したかを判定する
                // Check whether the target node was reached at distance zero
                var node = railPosition.GetNodeApproaching();
                if (node == null || _simulationTargetNode == null)
                {
                    return false;
                }
                return node.NodeGuid == _simulationTargetNode.NodeGuid && railPosition.GetDistanceToNextNode() == 0;
            }

            #endregion
        }

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
