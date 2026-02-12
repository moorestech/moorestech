using Game.Train.RailCalc;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
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

        public Guid TrainId { get; }
        public double CurrentSpeed { get; set; }
        public double AccumulatedDistance { get; set; }
        public int MasconLevel { get; set; }
        public bool IsAutoRun { get; set; }
        public bool IsDocked { get; set; }

        private IReadOnlyList<TrainCarSnapshot> _cars;
        // 車両スナップショットを外部に公開する
        // Expose car snapshots for render/update systems
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
            IsDocked = false;
        }

        // スナップショットの内容で内部状態を更新
        // Update internal state by the received snapshot
        public void SnapshotUpdate(TrainSimulationSnapshot simulation, RailPositionSaveData railPosition, long tick)
        {
            CurrentSpeed = simulation.CurrentSpeed;
            AccumulatedDistance = simulation.AccumulatedDistance;
            MasconLevel = simulation.MasconLevel;
            IsAutoRun = simulation.IsAutoRun;
            IsDocked = simulation.IsDocked;
            RailPosition = RailPositionFactory.Restore(railPosition, _railGraphProvider);
            _cars = simulation.Cars ?? Array.Empty<TrainCarSnapshot>();
            LastUpdatedTick = tick;

            UpdateSimulationTargetNodeBySnapshot();
            RecalculateRemainingDistance();
        }

        // 現在の状態からスナップショットバンドルを生成する
        // Build a snapshot bundle from the current client state
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
                    IsAutoRun,
                    IsDocked,
                    carSnapshots);
            }

            #endregion
        }

        // 1tickごとに呼ばれる。進んだ距離を返す
        // Called every tick and returns moved distance
        public int Update()
        {
            // 自動運転が有効で目標ノードがある場合のみ進める
            // Only advance when auto-run is active and target node exists
            if (!IsAutoRun || ResolveCurrentDestinationNode() == null)
            {
                return 0;
            }

            // ドッキング中は停止状態を維持する
            // Keep train stopped while docked
            if (IsDocked)
            {
                CurrentSpeed = 0;
                return 0;
            }

            // 目標ノードへ向けてマスコンを更新して移動する
            // Update mascon and move toward target node
            UpdateMasconLevel();
            var distanceToMove = SimulateMotionStep();
            return UpdateTrainByDistance(distanceToMove);

            #region Internal

            int SimulateMotionStep()
            {
                // 速度と距離のステップ計算
                // Simulate velocity and distance per tick
                var tractionForce = MasconLevel > 0 ? UpdateTractionForce(MasconLevel) : 0.0;
                var stepInput = new TrainMotionStepInput(CurrentSpeed, AccumulatedDistance, MasconLevel, tractionForce);
                var stepResult = TrainDistanceSimulator.Step(stepInput);
                CurrentSpeed = stepResult.NewSpeed;
                AccumulatedDistance = stepResult.NewAccumulatedDistance;
                return stepResult.DistanceToMove;
            }

            #endregion
        }

        // 自動運転時のマスコン制御を共通ロジックで更新
        // Update mascon level via shared auto-run calculation
        public void UpdateMasconLevel()
        {
            var input = new AutoRunMasconInput(CurrentSpeed, RemainingDistance);
            MasconLevel = TrainAutoRunMasconCalculator.Calculate(input);
        }

        // Updateの距離int版
        // distanceToMoveの距離絶対進む。進んだ距離を返す
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
                // Stop on target arrival and select next provisional target
                if (IsArrivedDestination() && IsAutoRun)
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

                if (IsAutoRun)
                {
                    var (found, newPath) = TryFindPathToSimulationTarget(approaching);
                    if (!found)
                    {
                        break;
                    }

                    RailPosition.AddNodeToHead(newPath[1]);
                    RemainingDistance = RailNodeCalculate.CalculateTotalDistanceF(newPath);
                }
                else
                {
                    var nextNodeList = approaching.ConnectedNodes.ToList();
                    if (nextNodeList.Count == 0)
                    {
                        CurrentSpeed = 0;
                        break;
                    }

                    RailPosition.AddNodeToHead(nextNodeList[0]);
                }

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
                // Check whether destination node is reached at zero distance
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
        // Compute traction while checking inventory-based train parameters
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
            // Recalculate the approximate remaining distance toward destination
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

        #endregion
    }
}
