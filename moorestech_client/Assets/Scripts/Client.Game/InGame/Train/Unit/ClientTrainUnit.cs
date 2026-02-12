using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.RailCalc;
using Game.Train.Unit;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Client.Game.InGame.Train.Unit
{
    // クライアント上で扱う最小限の列車データ
    // Minimal client-side representation of a train
    public sealed class ClientTrainUnit
    {
        private readonly IRailGraphProvider _railGraphProvider;
        private IRailNode _simulationTargetNode;
        private bool _isDockingStopPendingForTick;

        public Guid TrainId { get; }
        public double CurrentSpeed { get; set; }
        public double AccumulatedDistance { get; set; }
        public int MasconLevel { get; set; }

        private IReadOnlyList<TrainCarSnapshot> _cars;
        // 車両スナップショットを外部に公開する
        public IReadOnlyList<TrainCarSnapshot> Cars => _cars ?? Array.Empty<TrainCarSnapshot>();

        public RailPosition RailPosition { get; private set; }
        public long LastUpdatedTick { get; private set; }
        public int RemainingDistance { get; private set; } = int.MaxValue;

        public ClientTrainUnit(Guid trainId, IRailGraphProvider railGraphProvider)
        {
            // レールグラフプロバイダを保持する
            // Keep the rail graph provider reference
            _railGraphProvider = railGraphProvider;
            TrainId = trainId;
        }

        // スナップショットの内容で内部状態を更新
        // Update internal state by the received snapshot
        public void SnapshotUpdate(TrainSimulationSnapshot simulation, RailPositionSaveData railPosition, long tick)
        {
            CurrentSpeed = simulation.CurrentSpeed;
            AccumulatedDistance = simulation.AccumulatedDistance;
            MasconLevel = simulation.MasconLevel;
            RailPosition = RailPositionFactory.Restore(railPosition, _railGraphProvider);
            _cars = simulation.Cars ?? Array.Empty<TrainCarSnapshot>();
            LastUpdatedTick = tick;

            UpdateSimulationTargetNodeBySnapshot();
            RecalculateRemainingDistance();
        }

        // pre sim差分イベントを反映する
        // Apply pre-simulation diff values from the server.
        public void ApplyPreSimulationDiff(int masconLevelDiff, bool isNowDockingSpeedZero, int approachingNodeIdDiff, long tick)
        {
            MasconLevel += masconLevelDiff;
            if (approachingNodeIdDiff != 0)
            {
                UpdateSimulationTargetNodeBySnapshot();
                RecalculateRemainingDistance();
            }
            if (isNowDockingSpeedZero)
            {
                // このtickのシミュレーション内でドッキング停止処理を実行する
                _isDockingStopPendingForTick = true;
            }
            LastUpdatedTick = Math.Max(LastUpdatedTick, tick);
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
            // 進行先ノードが未確定の間はシミュレーションを進めない
            if (ResolveCurrentDestinationNode() == null)
            {
                if (_isDockingStopPendingForTick)
                {
                    _isDockingStopPendingForTick = false;
                    CurrentSpeed = 0;
                    AccumulatedDistance = 0;
                }
                return 0;
            }

            // サーバー通知済みMasconLevelで速度シミュレーションを進める
            // Simulate movement using the server-synchronized mascon level.
            if (_isDockingStopPendingForTick)
            {
                _isDockingStopPendingForTick = false;
                return SimulateDockingStopTick();
            }

            var distanceToMove = SimulateMotionStep();
            return UpdateTrainByDistance(distanceToMove);

            #region Internal

            int SimulateDockingStopTick()
            {
                // ドッキング確定tickでは目的地まで進めて残移動を捨てる
                if (!TryCalculateDistanceToSimulationTarget(out var forceMoveDistance))
                {
                    CurrentSpeed = 0;
                    AccumulatedDistance = 0;
                    return 0;
                }

                var moved = forceMoveDistance > 0 ? UpdateTrainByDistance(forceMoveDistance) : 0;
                CurrentSpeed = 0;
                AccumulatedDistance = 0;
                RemainingDistance = 0;
                return moved;
            }

            int SimulateMotionStep()
            {
                // 速度と距離のステップ計算
                var tractionForce = MasconLevel > 0 ? UpdateTractionForce(MasconLevel) : 0.0;
                var stepInput = new TrainMotionStepInput(CurrentSpeed, AccumulatedDistance, MasconLevel, tractionForce);
                var stepResult = TrainDistanceSimulator.Step(stepInput);
                CurrentSpeed = stepResult.NewSpeed;
                AccumulatedDistance = stepResult.NewAccumulatedDistance;
                return stepResult.DistanceToMove;
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
                RemainingDistance = Math.Max(0, RemainingDistance - moveLength);

                // 目標ノードに到達したら停止し、次目標を仮決定する
                if (IsArrivedDestination())
                {
                    CurrentSpeed = 0;
                    AccumulatedDistance = 0;

                    var destinationNode = ResolveCurrentDestinationNode();
                    if (destinationNode?.StationRef?.HasStation != true)
                    {
                        MoveSimulationTargetNodeToNext(destinationNode);
                        RecalculateRemainingDistance();
                    }

                    break;
                }

                if (distanceToMove == 0) break;

                var approaching = RailPosition.GetNodeApproaching();
                if (approaching == null)
                {
                    CurrentSpeed = 0;
                    break;
                }

                var (found, newPath) = TryFindPathToSimulationTarget(approaching);
                if (!found)
                {
                    break;
                }

                RailPosition.AddNodeToHead(newPath[1]);
                RemainingDistance = RailNodeCalculate.CalculateTotalDistanceF(newPath);

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
                var destinationNode = ResolveCurrentDestinationNode();
                if (node == null || destinationNode == null)
                {
                    return false;
                }

                return (node == destinationNode) && (RailPosition.GetDistanceToNextNode() == 0);
            }

            #endregion
        }

        // 毎フレーム燃料在庫を確認しながら加速力を計算する
        public double UpdateTractionForce(int masconLevel)
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

            #region Internal

            (int, int) GetWeightAndTraction(TrainCarSnapshot trainCarSnapshot)
            {
                return (TrainMotionParameters.DEFAULT_WEIGHT + trainCarSnapshot.InventorySlotsCount * TrainMotionParameters.WEIGHT_PER_SLOT, trainCarSnapshot.IsFacingForward ? trainCarSnapshot.TractionForce * TrainMotionParameters.DEFAULT_TRACTION : 0);
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

        #region Internal

        private IRailNode ResolveCurrentDestinationNode()
        {
            return _simulationTargetNode;
        }

        private void RecalculateRemainingDistance()
        {
            // 目的地までの概算距離を再計算
            var destinationNode = ResolveCurrentDestinationNode();
            var approaching = RailPosition?.GetNodeApproaching();
            if (destinationNode == null || approaching == null)
            {
                RemainingDistance = int.MaxValue;
                return;
            }

            if (ReferenceEquals(destinationNode, approaching))
            {
                RemainingDistance = RailPosition.GetDistanceToNextNode();
                return;
            }

            var path = _railGraphProvider.FindShortestPath(approaching, destinationNode);
            if (path == null || path.Count < 2)
            {
                RemainingDistance = int.MaxValue;
                return;
            }

            var tailDistance = RailNodeCalculate.CalculateTotalDistanceF(path);
            if (tailDistance < 0)
            {
                RemainingDistance = int.MaxValue;
                return;
            }

            RemainingDistance = RailPosition.GetDistanceToNextNode() + tailDistance;
        }

        private void UpdateSimulationTargetNodeBySnapshot()
        {
            // スナップショット適用時は接近ノードを暫定目標として採用する
            // Adopt approaching node as provisional target on snapshot apply
            _simulationTargetNode = RailPosition?.GetNodeApproaching();
            if (_simulationTargetNode == null)
            {
                return;
            }

            MoveSimulationTargetNodeToNext(_simulationTargetNode);
        }

        private void MoveSimulationTargetNodeToNext(IRailNode currentNode)
        {
            if (currentNode == null)
            {
                _simulationTargetNode = null;
                return;
            }

            var nextNodeList = currentNode.ConnectedNodes.ToList();
            if (nextNodeList.Count == 0)
            {
                _simulationTargetNode = currentNode;
                return;
            }

            _simulationTargetNode = nextNodeList[0];
        }

        private bool TryCalculateDistanceToSimulationTarget(out int distance)
        {
            distance = int.MaxValue;

            var destinationNode = ResolveCurrentDestinationNode();
            var approaching = RailPosition?.GetNodeApproaching();
            if (destinationNode == null || approaching == null)
            {
                return false;
            }

            if (ReferenceEquals(destinationNode, approaching))
            {
                distance = RailPosition.GetDistanceToNextNode();
                return true;
            }

            var path = _railGraphProvider.FindShortestPath(approaching, destinationNode);
            if (path == null || path.Count < 2)
            {
                return false;
            }

            var tailDistance = RailNodeCalculate.CalculateTotalDistanceF(path);
            if (tailDistance < 0)
            {
                return false;
            }

            distance = RailPosition.GetDistanceToNextNode() + tailDistance;
            return true;
        }


        #endregion
    }
}

