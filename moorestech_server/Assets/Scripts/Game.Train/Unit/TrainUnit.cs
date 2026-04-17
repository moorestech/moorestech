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
        private int _previousMasconLevel = 0;// キー入力と差分通知関連
        private bool _isDockingSpeedForcedToZero;//ドッキングした瞬間強制速度0になるのでmasconlevel差分通知ではズレが生じる
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
            _currentSpeed = 0.0; // 仮の初期速度
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
        
        public int Update()
        {
            return Update(TrainUnitManualCommand.Default);
        }
        
        // 1 tick ごとに呼ばれ、進んだ距離を返す。
        // Called once per tick and returns the traveled distance.
        public int Update(TrainUnitManualCommand manualCommand)
        {
            if (IsAutoRun)
            {
                // まず diagram の変更有無を確認する。
                // Check whether the current diagram is still valid.
                // 自動運転中に手動でダイアグラムをいじって目的地が null になった場合は自動運転を解除する。
                // Turn off auto-run when manual edits leave it without a destination.
                if (trainDiagram.GetCurrentNode() == null)
                {
                    TurnOffAutoRun();
                    _currentSpeed = 0;
                    return 0;
                }
                // 自動運転中はドッキング中なら進まず、ドッキング中でなければ目的地に向かって加速する。
                // During auto-run, stay stopped while docked and accelerate toward the destination otherwise.
                if (trainUnitStationDocking.IsDocked)
                {
                    _currentSpeed = 0;
                    masconLevel = 0;
                    trainUnitStationDocking.TickDockedStations();
                    // trainDiagram の出発条件を満たしたら、次の目的地をセットし次の tick で発車できるようにする。
                    // When the departure condition is satisfied, prepare the next destination so the next tick can depart.
                    trainDiagram.Update();
                    if (trainDiagram.CanCurrentEntryDepart())
                    {
                        // 次の目的地へ進める。
                        // Advance to the next destination entry.
                        DepartFromCurrentEntry();
                    }
                    return 0;
                }
                else
                {
                    // ドッキング中でなければ目的地に向かって進む。
                    // Move toward the destination when not docked.
                    UpdateMasconLevel();
                }
            }
            else
            {
                // 手動時にドッキング中ならまず解除する。
                // Undock first when manual control starts from a docked state.
                if (trainUnitStationDocking.IsDocked)
                {
                    // 強制ドッキング解除を行う。
                    // Force undocking.
                    trainUnitStationDocking.UndockFromStation();
                    return 0;
                }
                else
                {
                    // ドッキング中でなければ manual command を適用する。
                    // Apply the manual command when not docked.
                    ManualInput(manualCommand);
                }
            }

            // マスコンレベルから燃料を消費しつつ速度を計算する。
            // Consume fuel and update speed from the decided mascon level.
            // マスコン確定後に進む距離を算出する。
            // Calculate the travel distance after the mascon decision.
            var distanceToMove = SimulateMotionStep();
            return UpdateTrainByDistance(distanceToMove);
        }

        // キー操作系。
        // Manual input handling.
        public void ManualInput(TrainUnitManualCommand manualCommand) 
        {
            if (manualCommand.ReverseRequested && _currentSpeed == 0)
            {
                Reverse();
            }
            masconLevel = ConvertManualMasconCommandToMasconLevel(manualCommand.MasconCommand);
        }

        private static int ConvertManualMasconCommandToMasconLevel(int masconCommand)
        {
            // TrainUnit ローカル manual command を既存の masconLevel に変換する。
            // Convert TrainUnit-local manual commands into the legacy masconLevel scale.
            // +1 は traction の +16777216、0 は neutral の 0、-1 は brake の -16777216 を意味する。
            // +1 maps to traction +16777216, 0 to neutral 0, and -1 to brake -16777216.
            switch (masconCommand)
            {
                case -1:
                    return -MasterHolder.TrainUnitMaster.MasconLevelMaximum;
                case 0:
                    return 0;
                case 1:
                    return MasterHolder.TrainUnitMaster.MasconLevelMaximum;
                default:
                    throw new ArgumentOutOfRangeException(nameof(masconCommand), masconCommand, "Manual mascon command must be -1, 0, or 1.");
            }
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
        
        // Update の距離 int 版。
        // Integer-distance variant of Update.
        // distanceToMove の距離だけ絶対に進め、実際に進んだ距離を返す。
        // Advance by the requested distance and return the actual traveled amount.
        // 目的地は常に最新の trainDiagram を参照し、目的地が null なら上の制御で auto-run を解除する。
        // Always reference the latest train diagram, and let the outer flow disable auto-run when the destination becomes null.
        public int UpdateTrainByDistance(int distanceToMove) 
        {
            // 進行メインループ。
            // Main movement loop.
            int totalMoved = 0;
            // 想定外の経路状態で無限ループしないように上限回数を設ける。
            // Cap the loop count to avoid infinite movement loops on unexpected rail states.
            int loopCount = 0;
            while (true)
            {
                int moveLength = _railPosition.MoveForward(distanceToMove);
                distanceToMove -= moveLength;
                totalMoved += moveLength;
                _remainingDistance -= moveLength;
                // 自動運転で目的地に到着したらドッキング判定が必要になる。
                // Auto-run must evaluate docking once the destination is reached.
                if (_railPosition.DistanceToNextNode == 0)
                {
                    if (IsArrivedDestination() && _isAutoRun)
                    {
                        _currentSpeed = 0;
                        _accumulatedDistance = 0;
                        _isDockingSpeedForcedToZero = true;
                        _pendingApproachingNodeId = _railPosition.GetNodeApproaching()?.NodeId ?? -1;
                        // diagram が駅を見ている場合。
                        // When the diagram target is a station.
                        if (trainDiagram.GetCurrentNode().StationRef.StationBlock != null)
                        {
                            trainUnitStationDocking.TryDockWhenStopped();
                        }
                        else
                        {
                            // diagram が非駅を見ている場合は次の目的地へ進める。
                            // When the diagram target is not a station, advance to the next destination.
                            trainDiagram.NextEntryAndDepartureReset();
                        }
                        break;
                    }
                }
                if (distanceToMove == 0) break;
                //----------------------------------------------------------------------------------------
                // ここではまだ移動距離が残っており、分岐点または行き止まりに到達している。
                // Distance remains while the train is sitting on a junction or dead end.
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
                    // 経路が見つかったので最適なルートを自動選択する。
                    // Select the best available route automatically once a path is found.
                    _railPosition.AddNodeToHead(newPath[1]);//newPath[0]はapproachingがはいってる
                    _pendingApproachingNodeId = newPath[1].NodeId;
                    // 残りの距離を再計算する。
                    // Recompute the remaining distance.
                    _remainingDistance = RailNodeCalculate.CalculateTotalDistanceF(newPath);//計算量NlogN(logはnodeからintの辞書アクセス)
                }
                else
                {
                    // manual 時は approaching から最初の接続ノードを選んで先頭に積む。
                    // In manual mode, pick the first connected node from the approaching node.
                    var nextNodelist = approaching.ConnectedNodes.ToList();
                    if (nextNodelist.Count == 0)
                    {
                        _currentSpeed = 0;
                        break;//もう進めない
                    }
                    var nextNode = nextNodelist[0];
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

        // 毎フレーム燃料在庫を確認しながら加速力を計算する。
        // Calculate traction while checking fuel availability each frame.
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

        // diagram の現在目的地にちょうど 0 距離で到達したかを判定する。
        // Determine whether the current diagram destination has been reached exactly.
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
            // バリデーションで auto-run を止める条件を洗い出す。
            // Validate whether auto-run can stay enabled.
            _isAutoRun = true;
            DiagramValidation();
        }
        
        // masconLevel などの差分を抽出する。
        // Extract mascon and other per-tick diffs.
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
            // 目的地にすでに到達している場合は remainingDistance を更新する。
            // Refresh remaining distance when the destination is already the approaching node.
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
            // _remainingDistance を更新する。
            // Update the remaining distance cache.
            _remainingDistance = _railPosition.GetDistanceToNextNode() + RailNodeCalculate.CalculateTotalDistanceF(newPath);
        }

        public void TurnOffAutoRun()
        {
            _isAutoRun = false;
            _remainingDistance = int.MaxValue;
            masconLevel = 0;
            _accumulatedDistance = 0;
            // ドッキングしていたら解除する。
            // Undock if the train is currently docked.
            if (trainUnitStationDocking.IsDocked)
            {
                trainUnitStationDocking.UndockFromStation();
            }
        }

        // 現在の diagram current から順に見て、approaching から到達可能な entry node があれば true を返す。
        // Return true when any later diagram entry is reachable from the approaching node.
        public (bool, List<IRailNode>) CheckAllDiagramPath(IRailNode approaching) 
        {
            IRailNode destinationNode = null;
            List<IRailNode> newPath = null;
            // ダイアグラム上で次の目的地へ順送りし、全部の経路がなくなった場合は auto-run を解除する。
            // Advance through diagram entries and turn off auto-run when none remain reachable.
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
                // 経路が見つかったのでループを抜ける。
                // Exit once a reachable path is found.
                found = true;
                break;
            }
            return (found, newPath);
        }

        // 列車編成を保存する。ブロック保存とは別物である点に注意する。
        // Save the train formation state independently from block persistence.
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

        // 編成を分割連結する処理例。
        // Example helpers for splitting and attaching train formations.
        /// <summary>
        ///  分割
        ///  列車を「後ろから numberOfCarsToDetach 両」切り離して、後ろを新しいTrainUnitに。後半部分の部分を新しいTrainUnitとして返す 0両でも返す
        ///  新しいTrainUnitのrailpositionは、切り離した車両の長さに応じて調整される
        ///  新しいTrainUnitのtrainDiagramは空になる
        ///  新しいTrainUnitのドッキング状態はcarに情報があるためそのまま保存される
        /// </summary>
        
        public TrainUnit SplitTrain(int numberOfCarsToDetach)
        {
            // 例として 10 両を 5 両 + 5 両へ分割するイメージで処理する。
            // Conceptually, this handles cases like splitting 10 cars into 5 and 5.
            if (numberOfCarsToDetach <= 0 || numberOfCarsToDetach > _cars.Count)
            {
                if (numberOfCarsToDetach != 0)
                    Debug.LogWarning("SplitTrain: 指定両数が不正です。");
                return null;
            }
            TurnOffAutoRun();
            // 1) 切り離す車両リストを作成する。
            // 1) Build the list of cars to detach.
            // 後ろ側から numberOfCarsToDetach 両を取得する。
            // Take numberOfCarsToDetach cars from the rear.
            var detachedCars = _cars
                .Skip(_cars.Count - numberOfCarsToDetach)
                .ToList();
            // 3) 新しく後ろの TrainUnit を作る。
            // 3) Create a new rear TrainUnit.
            var splittedRailPosition = CreateSplittedRailPosition(detachedCars);
            // 2) 既存の TrainUnit からはそのぶん削除する。
            // 2) Remove the detached cars from the current TrainUnit.
            _cars.RemoveRange(_cars.Count - numberOfCarsToDetach, numberOfCarsToDetach);
            // _cars の両数に応じて列車長を算出する。
            // Recalculate train length from the remaining cars.
            int newTrainLength = 0;
            foreach (var car in _cars)
                newTrainLength += car.Length;
            _railPosition.SetTrainLength(newTrainLength);
            // 4) 新しい TrainUnit を作成する。
            // 4) Instantiate the new TrainUnit.
            var splittedUnit = new TrainUnit(splittedRailPosition, detachedCars, _railPositionManager, _diagramManager);
            // 6) 新しい TrainUnit を返す。
            // 6) Return the new TrainUnit.
            return splittedUnit;

            #region Internal
            /// <summary>
            /// 後続列車のために、新しいRailPositionを生成し返す。
            /// ここでは単純に列車の先頭からRailNodeの距離を調整するだけ
            /// </summary>
            RailPosition CreateSplittedRailPosition(List<TrainCar> splittedCars)
            {
                // _railPosition を deep copy する。
                // Deep-copy the current rail position.
                var newNodes = _railPosition.DeepCopy();
                // splittedCars の両数に応じて列車長を算出する。
                // Calculate the train length for the detached cars.
                int splittedTrainLength = 0;
                foreach (var car in splittedCars)
                    splittedTrainLength += car.Length;
                // 切り離した長さが列車の全長と一致した時点で、現状のまま戻す
                // Return as-is when the detached length matches the full train length
                if (splittedTrainLength == newNodes.TrainLength) return newNodes;
                // newNodes を反転して新しい列車長を設定する。
                // Reverse newNodes and set the detached train length.
                newNodes.Reverse();
                newNodes.SetTrainLength(splittedTrainLength);
                // もう一度反転して後ろ側の列車位置へ戻す。
                // Reverse again to place the detached train at the rear position.
                newNodes.Reverse();
                return newNodes;
            }
            #endregion
        }
        
        /// <summary>
        /// 指定 GUID or indexの列車両を安全に削除する
        /// Removes the train car that matches the given GUID.
        /// s=x+1+y両のtrainunitから1を削除しx,yのうちyのTrainUnitを返す関数。xはthis
        /// x,yは0以上
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
        /// 列車編成の先頭or最後尾に1両連結する
        /// 現時点での実装は、RailPosition railPositionはこの追加Carの前輪～後輪までの範囲をもったrailPositionが必要かつ
        /// そのどちらかは現時点のTrainUnitの先頭or最後尾に接続されている必要がある
        /// </summary>
        public void AttachCarToHead(TrainCar car, RailPosition railPosition)
        {
            // trainUnit の先頭連結なので、car の railPosition は car 前輪から trainUnit 先頭前輪までを指す必要がある。
            // For head attachment, the car railPosition must span from the car front axle to the train head front axle.
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
            // RailPosition を更新し、追加車両ぶんの距離を後端へ伸ばす。
            // Update RailPosition and extend the rear span for the attached car.
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