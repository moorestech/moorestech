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
        private int _remainingDistance;// 自動減速用
        private bool _isAutoRun;
        public bool IsAutoRun => _isAutoRun;
        private double _currentSpeed;   // m/s など適宜
        public double CurrentSpeed => _currentSpeed;
        private double _accumulatedDistance; // 累積距離、距離の小数点以下を保持するために使用
        internal double AccumulatedDistance => _accumulatedDistance;

        private List<TrainCar> _cars;
        public IReadOnlyList<TrainCar> Cars => _cars;
        IReadOnlyList<ITrainDiagramCar> ITrainDiagramContext.Cars => _cars;
        public RailPosition RailPosition => _railPosition;
        public TrainUnitStationDocking trainUnitStationDocking { get; private set; } // 列車の駅ドッキング用のクラス
        public TrainDiagram trainDiagram { get; private set; } // 列車のダイアグラム
        public bool IsDocked => trainUnitStationDocking?.IsDocked ?? false;
        public int masconLevel = 0;
        private int _previousMasconLevel;
        private bool _isDockingSpeedForcedToZero;//ドッキングした瞬間強制速度0になるのでmasconlevel差分通知ではズレが生じる
        private int _pendingApproachingNodeId;
        private bool _hasManualControl;
        private TrainManualCommand _manualControlCommand;
        public TrainUnit(
            RailPosition initialPosition,
            List<TrainCar> cars,
            TrainRailPositionManager railPositionManager,
            TrainDiagramManager diagramManager
        )
        {
            _railPosition = initialPosition;
            // Resolve the graph provider from the head node
            _railGraphProvider = initialPosition.GetNodeApproaching().GraphProvider;
            _railPositionManager = railPositionManager;
            _diagramManager = diagramManager;
            _railPositionManager.RegisterRailPosition(_railPosition);
            _trainInstanceId = TrainInstanceId.Create();
            _cars = cars;
            _currentSpeed = 0.0; // 仮の初期速度
            _isAutoRun = false;
            trainUnitStationDocking = new TrainUnitStationDocking(this, this);
            trainDiagram = new TrainDiagram(_railGraphProvider, _diagramManager);
            trainDiagram.SetContext(this);
            ClearManualControl();
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

        public int Update()
        {
            if (IsAutoRun)
            {
                if (trainDiagram.GetCurrentNode() == null)
                {
                    TurnOffAutoRun();
                    _currentSpeed = 0;
                    return 0;
                }
                if (trainUnitStationDocking.IsDocked)
                {
                    _currentSpeed = 0;
                    masconLevel = 0;
                    trainUnitStationDocking.TickDockedStations();
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
            else if (_hasManualControl)
            {
                if (trainUnitStationDocking.IsDocked)
                {
                    if (!_manualControlCommand.HasThrottle)
                    {
                        _currentSpeed = 0;
                        masconLevel = 0;
                        trainUnitStationDocking.TickDockedStations();
                        return 0;
                    }

                    trainUnitStationDocking.UndockFromStation();
                }

                KeyInput(_manualControlCommand);
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
                    KeyInput(TrainManualCommand.Inactive);
                }
            }

            // Calculate distance to travel after mascon decision
            var distanceToMove = SimulateMotionStep();
            return UpdateTrainByDistance(distanceToMove);
        }

        public void KeyInput(TrainManualCommand command)
        {
            if (command.ThrottleNotch > 0)
            {
                masconLevel = MasterHolder.TrainUnitMaster.MasconLevelMaximum;
                return;
            }

            if (command.ThrottleNotch < 0)
            {
                masconLevel = -MasterHolder.TrainUnitMaster.MasconLevelMaximum;
                return;
            }

            masconLevel = 0;
        }

        private void DepartFromCurrentEntry()
        {
            if (trainUnitStationDocking.IsDocked)
            {
                trainUnitStationDocking.UndockFromStation();
            }
            trainDiagram.NextEntryAndDepartureReset();
        }

        // Update mascon level via shared auto-run calculation
        public void UpdateMasconLevel()
        {
            var input = new AutoRunMasconInput(
                _currentSpeed,
                _remainingDistance);
            masconLevel = TrainAutoRunMasconCalculator.Calculate(input);
        }

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
                        else//diagramが非駅を見ている場合
                        {
                            trainDiagram.NextEntryAndDepartureReset();
                        }
                        break;
                    }
                }
                if (distanceToMove == 0) break;
                //----------------------------------------------------------------------------------------
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
                    if (!found)//全部の経路がなくなった
                    {
                        Debug.Log("diagramの登録nodeに対する経路が全てなくなった。自動運転off");
                        TurnOffAutoRun();
                        break;
                    }
                    _railPosition.AddNodeToHead(newPath[1]);//newPath[0]はapproachingがはいってる
                    _pendingApproachingNodeId = newPath[1].NodeId;
                    _remainingDistance = RailNodeCalculate.CalculateTotalDistanceF(newPath);//計算量NlogN(logはnodeからintの辞書アクセス)
                }
                else
                {
                    var nextNodelist = approaching.ConnectedNodes.ToList();
                    if (nextNodelist.Count == 0)
                    {
                        _currentSpeed = 0;
                        break;//もう進めない
                    }
                    var nextNode = ResolveManualNextNode(nextNodelist);
                    _railPosition.AddNodeToHead(nextNode);
                    _pendingApproachingNodeId = nextNode.NodeId;
                }
                //----------------------------------------------------------------------------------------
                loopCount++;
                if (loopCount > InfiniteLoopGuardThreshold)
                {
                    throw new InvalidOperationException("列車速度が無限に近いか、レール経路の無限ループを検知しました。");
                }
            }
            return totalMoved;
        }

        private IRailNode ResolveManualNextNode(IReadOnlyList<IRailNode> nextNodes)
        {
            if (nextNodes == null || nextNodes.Count == 0)
            {
                return null;
            }

            if (_manualControlCommand.SteeringDirection < 0)
            {
                return nextNodes[0];
            }

            if (_manualControlCommand.SteeringDirection > 0)
            {
                return nextNodes[nextNodes.Count - 1];
            }

            return nextNodes[0];
        }

        public double UpdateTractionForce(int masconLevel)
        {
            int totalWeight = 0;
            int totalTraction = 0;
            foreach (var car in _cars)
            {
                car.ConsumeFuel(GameUpdater.SecondsPerTick, masconLevel);
                var (weight, traction) = car.GetWeightAndTraction(masconLevel); //forceに応じて燃料が消費される:未実装
                totalWeight += weight;
                totalTraction += traction;
            }
            return (double)totalTraction / totalWeight * masconLevel / MasterHolder.TrainUnitMaster.MasconLevelMaximum;
        }

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

        public void SetManualControl(in TrainManualCommand command)
        {
            _hasManualControl = command.IsActive;
            _manualControlCommand = command;

            if (_hasManualControl && _isAutoRun)
            {
                DisableAutoRunForManualControl();
            }
        }

        public void ClearManualControl()
        {
            _hasManualControl = false;
            _manualControlCommand = TrainManualCommand.Inactive;
        }

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
            if (approaching == destinationNode)
            {
                _remainingDistance = _railPosition.GetDistanceToNextNode();
                return;
            }
            var (found, newPath) = CheckAllDiagramPath(approaching);
            if (!found)//全部の経路がなくなった
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

        private void DisableAutoRunForManualControl()
        {
            _isAutoRun = false;
            _remainingDistance = int.MaxValue;
            masconLevel = 0;
        }

        public (bool, List<IRailNode>) CheckAllDiagramPath(IRailNode approaching)
        {
            IRailNode destinationNode = null;
            List<IRailNode> newPath = null;
            bool found = false;
            for (int i = 0; i < trainDiagram.Entries.Count; i++)
            {
                destinationNode = trainDiagram.GetCurrentNode();
                if (destinationNode == null)
                    break;//なにかの例外
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

        //============================================================
        //============================================================
        /// <summary>
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
            /// </summary>
            RailPosition CreateSplittedRailPosition(List<TrainCar> splittedCars)
            {
                var newNodes = _railPosition.DeepCopy();
                int splittedTrainLength = 0;
                foreach (var car in splittedCars)
                    splittedTrainLength += car.Length;
                // Return as-is when the detached length matches the full train length
                if (splittedTrainLength == newNodes.TrainLength) return newNodes;
                newNodes.Reverse();
                newNodes.SetTrainLength(splittedTrainLength);
                newNodes.Reverse();
                return newNodes;
            }
            #endregion
        }

        /// <summary>
        /// Removes the train car that matches the given GUID.
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

        public void AttachCarToRear(TrainCar car, RailPosition railPosition)//trainUnitの最後尾にcarが連結するのでcarのrailPositionはtrainunitの最後尾～carの後輪までを指してないといけない
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

