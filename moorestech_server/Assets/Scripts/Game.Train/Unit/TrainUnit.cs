using System;
using System.Collections.Generic;
using System.Linq;
using Core.Update;
using Game.Context;
using Game.Train.Diagram;
using Game.Train.RailCalc;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Core.Master;
using UnityEngine;

namespace Game.Train.Unit
{
    /// <summary>
    /// 複数車両からなる列車編成全体を表すクラス
    /// Represents an entire train formation composed of multiple cars.
    /// </summary>
    public class TrainUnit : ITrainDiagramContext, ITrainUnitStationDockingListener
    {
        private const int InfiniteLoopGuardThreshold = 1_000_000;
        
        private RailPosition _railPosition;
        private readonly IRailGraphProvider _railGraphProvider;
        private readonly TrainRailPositionManager _railPositionManager;
        private readonly TrainDiagramManager _diagramManager;
        private TrainInstanceId _trainInstanceId;
        public TrainInstanceId TrainInstanceId => _trainInstanceId;
        private int _remainingDistance;
        private bool _isAutoRun;
        public bool IsAutoRun => _isAutoRun;
        private double _currentSpeed;
        public double CurrentSpeed => _currentSpeed;
        private double _accumulatedDistance;
        internal double AccumulatedDistance => _accumulatedDistance;

        private List<TrainCar> _cars;
        public IReadOnlyList<TrainCar> Cars => _cars;
        IReadOnlyList<ITrainDiagramCar> ITrainDiagramContext.Cars => _cars;
        public RailPosition RailPosition => _railPosition;
        public TrainUnitStationDocking trainUnitStationDocking { get; private set; }
        public TrainDiagram trainDiagram { get; private set; }
        public bool IsDocked => trainUnitStationDocking?.IsDocked ?? false;
        // Manual control and diff state.
        public int masconLevel = 0;
        private int _manualMasconLevel;
        private int _manualBranchPreference;
        private int _previousMasconLevel;
        private bool _isDockingSpeedForcedToZero;
        private int _pendingApproachingNodeId;
        public TrainUnit(
            RailPosition initialPosition,
            List<TrainCar> cars,
            TrainRailPositionManager railPositionManager,
            TrainDiagramManager diagramManager
        )
        {
            _railPosition = initialPosition;
            // 先頭ノードからグラフプロバイダを取得する
            // Resolve the graph provider from the head node
            _railGraphProvider = initialPosition.GetNodeApproaching().GraphProvider;
            _railPositionManager = railPositionManager;
            _diagramManager = diagramManager;
            _railPositionManager.RegisterRailPosition(_railPosition);
            _trainInstanceId = TrainInstanceId.Create();
            _cars = cars;
            _currentSpeed = 0.0;
            _isAutoRun = false;
            trainUnitStationDocking = new TrainUnitStationDocking(this, this);
            trainDiagram = new TrainDiagram(_railGraphProvider, _diagramManager);
            trainDiagram.SetContext(this);
            ResetDiff();
        }

        public void Reverse()
        {
            _railPosition?.Reverse();
            if (_cars == null)
                return;
            _cars.Reverse();
            foreach (var car in _cars)
            {
                car.Reverse();
            }
        }
        public void ResetDiff()
        {
            _previousMasconLevel = masconLevel;
            _isDockingSpeedForcedToZero = false;
            _pendingApproachingNodeId = -1;
        }
        
        // Advance the train by one simulation tick and return moved distance.
        public int Update()
        {
            if (IsAutoRun)
            {
                // Turn off auto-run if the current destination disappears.
                if (trainDiagram.GetCurrentNode() == null)
                {
                    TurnOffAutoRun();
                    _currentSpeed = 0;
                    return 0;
                }
                // Auto-run either waits while docked or accelerates toward the target.
                if (trainUnitStationDocking.IsDocked)
                {
                    _currentSpeed = 0;
                    masconLevel = 0;
                    trainUnitStationDocking.TickDockedStations();
                    // Move to the next entry when the current one can depart.
                    trainDiagram.Update();
                    if (trainDiagram.CanCurrentEntryDepart())
                    {
                        DepartFromCurrentEntry();
                    }
                    return 0;
                }
                else
                {
                    UpdateMasconLevel();
                }
            }
            else 
            {
                if (trainUnitStationDocking.IsDocked)
                {
                    trainUnitStationDocking.UndockFromStation();
                    return 0;
                }
                else
                {
                    KeyInput();
                }
            }

            // Calculate speed and distance from the current mascon decision.
            var distanceToMove = SimulateMotionStep();
            return UpdateTrainByDistance(distanceToMove);
        }

        // Apply manual control state to the public mascon value.
        public void KeyInput() 
        {
            var maxMasconLevel = MasterHolder.TrainUnitMaster.MasconLevelMaximum;
            if (_manualMasconLevel > maxMasconLevel)
            {
                masconLevel = maxMasconLevel;
                return;
            }

            if (_manualMasconLevel < -maxMasconLevel)
            {
                masconLevel = -maxMasconLevel;
                return;
            }

            masconLevel = _manualMasconLevel;
        }

        public void SetManualInput(TrainManualOperation operation)
        {
            _manualMasconLevel = operation.MasconLevel;
            _manualBranchPreference = operation.BranchPreference;
        }

        public void ClearManualInput()
        {
            _manualMasconLevel = 0;
            _manualBranchPreference = 0;
        }

        private void DepartFromCurrentEntry()
        {
            if (trainUnitStationDocking.IsDocked)
            {
                trainUnitStationDocking.UndockFromStation();
            }
            trainDiagram.NextEntryAndDepartureReset();
        }

        // 自動運転時のマスコン制御を共通ロジックで更新
        // Update mascon level via shared auto-run calculation
        public void UpdateMasconLevel()
        {
            var input = new AutoRunMasconInput(
                _currentSpeed,
                _remainingDistance);
            masconLevel = TrainAutoRunMasconCalculator.Calculate(input);
        }

        // 速度と距離のステップ計算
        // Simulate velocity and distance per tick
        private int SimulateMotionStep()
        {
            var tractionForce = masconLevel > 0 ? UpdateTractionForce(masconLevel) : 0.0;
            var stepInput = new TrainMotionStepInput(
                _currentSpeed,
                _accumulatedDistance,
                masconLevel,
                tractionForce);
            var stepResult = TrainDistanceSimulator.Step(stepInput);
            _currentSpeed = stepResult.NewSpeed;
            _accumulatedDistance = stepResult.NewAccumulatedDistance;
            return stepResult.DistanceToMove;
        }
        
        // Move the train along the rail graph and return the total moved distance.
        public int UpdateTrainByDistance(int distanceToMove) 
        {
            int totalMoved = 0;
            int loopCount = 0;
            while (true)
            {
                int moveLength = _railPosition.MoveForward(distanceToMove);
                distanceToMove -= moveLength;
                totalMoved += moveLength;
                _remainingDistance -= moveLength;
                // Auto-run trains may need to dock or advance the diagram at the target.
                if (_railPosition.DistanceToNextNode == 0)
                {
                    if (IsArrivedDestination() && _isAutoRun)
                    {
                        _currentSpeed = 0;
                        _accumulatedDistance = 0;
                        _isDockingSpeedForcedToZero = true;
                        _pendingApproachingNodeId = _railPosition.GetNodeApproaching()?.NodeId ?? -1;
                        if (trainDiagram.GetCurrentNode().StationRef.StationBlock != null)
                        {
                            trainUnitStationDocking.TryDockWhenStopped();
                        }
                        else
                        {
                            trainDiagram.NextEntryAndDepartureReset();
                        }
                        break;
                    }
                }
                if (distanceToMove == 0) break;
                // Reached a branch or dead end while there is still distance to consume.
                var approaching = _railPosition.GetNodeApproaching();
                if (approaching == null) 
                {
                    TurnOffAutoRun();
                    _currentSpeed = 0;
                    throw new InvalidOperationException("列車が進行中に接近しているノードがnullになりました。");
                }

                if (IsAutoRun)
                {
                    var (found, newPath) = CheckAllDiagramPath(approaching);
                    if (!found)
                    {
                        Debug.Log("diagramの登録nodeに対する経路が全てなくなった。自動運転off");
                        TurnOffAutoRun();
                        break;
                    }
                    _railPosition.AddNodeToHead(newPath[1]);
                    _pendingApproachingNodeId = newPath[1].NodeId;
                    _remainingDistance = RailNodeCalculate.CalculateTotalDistanceF(newPath);
                }
                else
                {
                    var nextNodelist = approaching.ConnectedNodes.ToList();
                    if (nextNodelist.Count == 0)
                    {
                        _currentSpeed = 0;
                        break;
                    }
                    var nextNode = SelectManualNextNode(nextNodelist);
                    _railPosition.AddNodeToHead(nextNode);
                    _pendingApproachingNodeId = nextNode.NodeId;
                }
                loopCount++;
                if (loopCount > InfiniteLoopGuardThreshold)
                {
                    throw new InvalidOperationException("列車速度が無限に近いか、レール経路の無限ループを検知しました。");
                }
            }
            return totalMoved;
        }

        private IRailNode SelectManualNextNode(IReadOnlyList<IRailNode> nextNodeList)
        {
            if (nextNodeList == null || nextNodeList.Count == 0)
            {
                return null;
            }

            if (_manualBranchPreference > 0 && nextNodeList.Count > 1)
            {
                return nextNodeList[nextNodeList.Count - 1];
            }

            return nextNodeList[0];
        }

        // Consume fuel and compute traction for the current tick.
        public double UpdateTractionForce(int masconLevel)
        {
            int totalWeight = 0;
            int totalTraction = 0;
            foreach (var car in _cars)
            {
                car.ConsumeFuel(GameUpdater.SecondsPerTick, masconLevel);
                var (weight, traction) = car.GetWeightAndTraction(masconLevel);
                totalWeight += weight;
                totalTraction += traction;
            }
            return (double)totalTraction / totalWeight * masconLevel / MasterHolder.TrainUnitMaster.MasconLevelMaximum;
        }

        // Check whether the approaching node matches the current destination.
        private bool IsArrivedDestination()
        {
            var node = _railPosition.GetNodeApproaching();
            var dnode = trainDiagram.GetCurrentNode();
            if (dnode == null) return false;
            if (node.NodeGuid == trainDiagram.GetCurrentNode().NodeGuid && _railPosition.GetDistanceToNextNode() == 0)
            {
                return true;
            }
            return false;
        }

        public void TurnOnAutoRun()
        {
            _isAutoRun = true;
            DiagramValidation();
        }
        
        // Collect diff data that should be broadcast for this tick.
        public (int,bool,int) GetTickDiff()
        {
            var ret1 = masconLevel - _previousMasconLevel;
            _previousMasconLevel = masconLevel;
            var ret2 = _isDockingSpeedForcedToZero;
            _isDockingSpeedForcedToZero = false;
            var ret3 = _pendingApproachingNodeId;
            _pendingApproachingNodeId = -1;
            return (ret1, ret2, ret3);
        }

        public void DiagramValidation() 
        {
            var destinationNode = trainDiagram.GetCurrentNode();
            var approaching = _railPosition.GetNodeApproaching();
            if ((destinationNode == null) || (approaching == null))
            {
                TurnOffAutoRun();
                return;
            }
            // Recompute remaining distance when already sitting on the destination node.
            if (approaching == destinationNode)
            {
                _remainingDistance = _railPosition.GetDistanceToNextNode();
                return;
            }
            var (found, newPath) = CheckAllDiagramPath(approaching);
            if (!found)
            {
                Debug.Log("diagramの登録nodeに対する経路が全てない。自動運転off");
                TurnOffAutoRun();
                return;
            }
            _remainingDistance = _railPosition.GetDistanceToNextNode() + RailNodeCalculate.CalculateTotalDistanceF(newPath);
        }

        public void TurnOffAutoRun()
        {
            _isAutoRun = false;
            _remainingDistance = int.MaxValue;
            masconLevel = 0;
            _accumulatedDistance = 0;
            if (trainUnitStationDocking.IsDocked)
            {
                trainUnitStationDocking.UndockFromStation();
            }
        }

        // Find the first diagram entry that is still reachable from the current node.
        public (bool, List<IRailNode>) CheckAllDiagramPath(IRailNode approaching) 
        {
            IRailNode destinationNode = null;
            List<IRailNode> newPath = null;
            bool found = false;
            for (int i = 0; i < trainDiagram.Entries.Count; i++)
            {
                destinationNode = trainDiagram.GetCurrentNode();
                if (destinationNode == null)
                    break;
                var path = _railGraphProvider.FindShortestPath(approaching, destinationNode);
                newPath = path?.ToList();
                if (newPath == null || newPath.Count < 2)
                {
                    trainDiagram.MoveToNextEntry();
                    continue;
                }
                found = true;
                break;
            }
            return (found, newPath);
        }

        // Save the full train formation state.
        public TrainUnitSaveData CreateSaveData()
        {
            var railpositionSnapshot = _railPosition.CreateSaveSnapshot();

            var carStates = new List<TrainCarSaveData>(); 
            foreach (var car in _cars)
            {
                var carData = car.CreateTrainCarSaveData();
                carStates.Add(carData);
            }

            var diagramState = trainDiagram.CreateTrainDiagramSaveData();

            return new TrainUnitSaveData
            {
                railPositionSaveData = railpositionSnapshot,
                IsAutoRun = _isAutoRun,
                CurrentSpeedBits = BitConverter.DoubleToInt64Bits(_currentSpeed),
                AccumulatedDistanceBits = BitConverter.DoubleToInt64Bits(_accumulatedDistance),
                Cars = carStates,
                Diagram = diagramState
            };
        }

        private static List<TrainCar> RestoreTrainCars(List<TrainCarSaveData> carData)
        {
            var cars = new List<TrainCar>();
            if (carData == null)
            {
                return cars;
            }
            foreach (var data in carData)
            {
                var car = TrainCar.RestoreTrainCar(data);
                if (car != null)
                {
                    cars.Add(car);
                }
            }
            return cars;
        }

        public static TrainUnit RestoreFromSaveData(TrainUnitSaveData saveData)
        {
            if (saveData == null)
                return null;

            var railPosData = saveData.railPositionSaveData;
            // サーバー側プロバイダでRailPositionを復元する
            // Restore RailPosition via the server-side provider
            var railGraphProvider = ServerContext.GetService<IRailGraphProvider>();
            var railPosition = RailPositionFactory.Restore(railPosData, railGraphProvider);
            if (railPosition == null)
                return null;
            var cars = RestoreTrainCars(saveData.Cars);

            var restoredSpeed = saveData.CurrentSpeedBits.HasValue
                ? BitConverter.Int64BitsToDouble(saveData.CurrentSpeedBits.Value)
                : 0;
            var restoredAccumulatedDistance = saveData.AccumulatedDistanceBits.HasValue
                ? BitConverter.Int64BitsToDouble(saveData.AccumulatedDistanceBits.Value)
                : 0;

            var railPositionManager = ServerContext.GetService<TrainRailPositionManager>();
            var diagramManager = ServerContext.GetService<TrainDiagramManager>();

            var trainUnit = new TrainUnit(railPosition, cars, railPositionManager, diagramManager);
            trainUnit._isAutoRun = saveData.IsAutoRun;
            trainUnit._currentSpeed = restoredSpeed;
            trainUnit._accumulatedDistance = restoredAccumulatedDistance;

            trainUnit._remainingDistance = trainUnit._railPosition.GetDistanceToNextNode();
            trainUnit.trainDiagram.RestoreState(saveData.Diagram);

            if (trainUnit._isAutoRun)
            {
                trainUnit.DiagramValidation();
            }
            return trainUnit;
        }

        // Split and attach helpers.
        /// <summary>
        /// Detach cars from the rear and return them as a new train unit.
        /// </summary>
        
        public TrainUnit SplitTrain(int numberOfCarsToDetach)
        {
            if (numberOfCarsToDetach <= 0 || numberOfCarsToDetach > _cars.Count)
            {
                if (numberOfCarsToDetach != 0)
                    Debug.LogWarning("SplitTrain: 指定両数が不正です。");
                return null;
            }
            TurnOffAutoRun();
            var detachedCars = _cars
                .Skip(_cars.Count - numberOfCarsToDetach)
                .ToList();
            var splittedRailPosition = CreateSplittedRailPosition(detachedCars);
            _cars.RemoveRange(_cars.Count - numberOfCarsToDetach, numberOfCarsToDetach);
            int newTrainLength = 0;
            foreach (var car in _cars)
                newTrainLength += car.Length;
            _railPosition.SetTrainLength(newTrainLength);
            var splittedUnit = new TrainUnit(splittedRailPosition, detachedCars, _railPositionManager, _diagramManager);
            return splittedUnit;

            #region Internal
            /// <summary>
            /// Create a rail position for the detached rear unit.
            /// </summary>
            RailPosition CreateSplittedRailPosition(List<TrainCar> splittedCars)
            {
                var newNodes = _railPosition.DeepCopy();
                int splittedTrainLength = 0;
                foreach (var car in splittedCars)
                    splittedTrainLength += car.Length;
                if (splittedTrainLength == newNodes.TrainLength) return newNodes;
                newNodes.Reverse();
                newNodes.SetTrainLength(splittedTrainLength);
                newNodes.Reverse();
                return newNodes;
            }
            #endregion
        }
        
        /// <summary>
        /// Remove a car by index and return the detached rear unit when present.
        /// </summary>
        public TrainUnit RemoveCar(int targetIndex)
        {
            if (targetIndex < 0 || targetIndex >= _cars.Count)
            {
                Debug.LogWarning($"RemoveCar: carIndex {targetIndex} is not found.");
                return null;
            }
            var carsBehind = _cars.Count - targetIndex - 1;
            var splitedTrainFirst = SplitTrain(carsBehind);
            var splitedTrainSecond = SplitTrain(1);
            splitedTrainSecond?.OnDestroy();
            return splitedTrainFirst;
        }
        public TrainUnit RemoveCar(TrainCarInstanceId trainCarInstanceId)
        {
            var targetIndex = _cars.FindIndex(car => car.TrainCarInstanceId == trainCarInstanceId);
            return RemoveCar(targetIndex);
        }


        /// <summary>
        /// Attach a single car to the head or tail of the formation.
        /// </summary>
        public void AttachCarToHead(TrainCar car, RailPosition railPosition)
        {
            if (!_railPosition.GetHeadRailPosition().IsSamePositionAllowNodeOverlap(railPosition.GetRearRailPosition()))
                throw new InvalidOperationException("trainUnitの先頭にcarが連結するのでcarのrailPositionはcarの前輪～trainUnitの先頭前輪までを指してないといけない");
            Reverse();
            railPosition.Reverse();
            car.Reverse();
            AttachCarToRear(car, railPosition);
            Reverse();
        }

        public void AttachCarToRear(TrainCar car, RailPosition railPosition)
        {
            if (!railPosition.GetHeadRailPosition().IsSamePositionAllowNodeOverlap(_railPosition.GetRearRailPosition()))
                throw new InvalidOperationException("trainUnitの最後尾にcarが連結するのでcarのrailPositionはtrainunitの最後尾～carの後輪までを指してないといけない");
            _cars.Add(car);
            _railPosition.AppendRailPositionAtRear(railPosition);
        }

        public void OnTrainDocked()
        {
        }
        public void OnTrainUndocked()
        {
        }

        public void OnCurrentEntryShiftedByRemoval()
        {
            if (!trainUnitStationDocking.IsDocked)
            {
                return;
            }
            trainUnitStationDocking.UndockFromStation();
        }

        public void OnDestroy()
        {
            _railPositionManager.UnregisterRailPosition(_railPosition);
            trainDiagram.OnDestroy();
            _railPosition.OnDestroy();
            trainUnitStationDocking.OnDestroy();

            foreach (var car in _cars)
            {
                car.Destroy();
            }
            _cars.Clear();
            
            trainDiagram = null;
            _railPosition = null;
            trainUnitStationDocking = null;
            _cars = null;
            _trainInstanceId = TrainInstanceId.Empty;
        }
    }
}


