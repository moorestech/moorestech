using Game.Context;
using Game.Train.Diagram;
using Game.Train.RailGraph;
using Game.Train.RailCalc;
using Game.Train.RailPositions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Train.Unit
{
    /// <summary>
    /// 複数車両からなる列車編成全体を表すクラス
    /// Represents an entire train formation composed of multiple cars.
    /// </summary>
    public class TrainUnit : ITrainDiagramContext, ITrainUnitStationDockingListener
    {
        public string SaveKey { get; } = typeof(TrainUnit).FullName;
        
        private RailPosition _railPosition;
        private readonly IRailGraphProvider _railGraphProvider;
        private readonly TrainUpdateService _trainUpdateService;
        private readonly TrainRailPositionManager _railPositionManager;
        private readonly TrainDiagramManager _diagramManager;
        private Guid _trainId;
        public Guid TrainId => _trainId;

        private int _remainingDistance;// 自動減速用
        private bool _isAutoRun;
        public bool IsAutoRun
        {
            get { return _isAutoRun; }
        }

        private double _currentSpeed;   // m/s など適宜
        public double CurrentSpeed => _currentSpeed;
        private double _accumulatedDistance; // 累積距離、距離の小数点以下を保持するために使用
        internal double AccumulatedDistance => _accumulatedDistance;
        private const int InfiniteLoopGuardThreshold = 1_000_000;

        private List<TrainCar> _cars;
        public RailPosition RailPosition => _railPosition;
        public IReadOnlyList<TrainCar> Cars => _cars;
        IReadOnlyList<ITrainDiagramCar> ITrainDiagramContext.Cars => _cars;
        public TrainUnitStationDocking trainUnitStationDocking { get; private set; } // 列車の駅ドッキング用のクラス
        public TrainDiagram trainDiagram { get; private set; } // 列車のダイアグラム
        public bool IsDocked => trainUnitStationDocking?.IsDocked ?? false;
        //キー関連
        //マスコンレベル 0がニュートラル、1が前進1段階、-1が後退1段階.キー入力やテスト、外部から直接制御できる。min maxは±16777216とする(暫定)
        public int masconLevel = 0;
        private int tickCounter = 0;// TODO デバッグトグル関係　そのうち消す
        public TrainUnit(
            RailPosition initialPosition,
            List<TrainCar> cars,
            TrainUpdateService trainUpdateService,
            TrainRailPositionManager railPositionManager,
            TrainDiagramManager diagramManager
        ) : this(initialPosition, cars, trainUpdateService, railPositionManager, diagramManager, true)
        {
        }

        private TrainUnit(
            RailPosition initialPosition,
            List<TrainCar> cars,
            TrainUpdateService trainUpdateService,
            TrainRailPositionManager railPositionManager,
            TrainDiagramManager diagramManager,
            bool notifyOnRegister
        )
        {
            _railPosition = initialPosition;
            // 先頭ノードからグラフプロバイダを取得する
            // Resolve the graph provider from the head node
            _railGraphProvider = initialPosition.GetNodeApproaching().GraphProvider;
            _trainUpdateService = trainUpdateService;
            _railPositionManager = railPositionManager;
            _diagramManager = diagramManager;
            _railPositionManager.RegisterRailPosition(_railPosition);
            _trainId = Guid.NewGuid();
            _cars = cars;
            _currentSpeed = 0.0; // 仮の初期速度
            _isAutoRun = false;
            trainUnitStationDocking = new TrainUnitStationDocking(this, this);
            trainDiagram = new TrainDiagram(_railGraphProvider, _diagramManager);
            trainDiagram.SetContext(this);
            // 列車登録と生成通知の制御を行う
            // Register the train and control creation notification
            RegisterTrain(notifyOnRegister);

            #region Internal
            void RegisterTrain(bool notify)
            {
                // 通知あり/なしで登録先を切り替える
                // Switch registration based on notification flag
                if (notify)
                {
                    _trainUpdateService.RegisterTrain(this);
                    return;
                }
                _trainUpdateService.RegisterTrainWithoutNotify(this);
            }
            #endregion
        }


        public void Reverse()
        {
            _railPosition?.Reverse();
            if (_cars == null)
            {
                return;
            }

            _cars.Reverse();
            foreach (var car in _cars)
            {
                car.Reverse();
            }
        }


        //1tickごとに呼ばれる.進んだ距離を返す?
        public int Update()
        {
            //数十回に1回くらいの頻度でデバッグログを出す TODO diagramAutoRunを実装したらけす
            tickCounter++;
            if (_trainUpdateService.IsTrainAutoRunDebugEnabled() && tickCounter % 20 == 0)
            {
                UnityEngine.Debug.Log("spd=" + _currentSpeed + "_Auto=" + IsAutoRun + "_DiagramCount" + trainDiagram.Entries.Count + "" + IsDocked);// TODO デバッグトグル関係　そのうち消す
            }

            if (IsAutoRun)
            {
                //まずdiagramの変更有無を確認する
                // 自動運転中に手動でダイアグラムをいじって目的地がnullになった場合は自動運転を解除する
                if (trainDiagram.GetCurrentNode() == null)
                {
                    //UnityEngine.Debug.Log("自動運転中に手動でダイアグラムをいじって目的地がnullになったので自動運転を解除");
                    TurnOffAutoRun();
                    _currentSpeed = 0;
                    return 0;
                }
                // 自動運転中はドッキング中なら進まない、ドッキング中じゃないなら目的地に向かって加速
                if (trainUnitStationDocking.IsDocked)
                {
                    _currentSpeed = 0;
                    if (_trainUpdateService.IsTrainAutoRunDebugEnabled() && tickCounter % 20 == 0)
                        UnityEngine.Debug.Log("ドッキング中");// TODO デバッグトグル関係　そのうち消す
                    trainUnitStationDocking.TickDockedStations();
                    // もしtrainDiagramの出発条件を満たしていたら、trainDiagramは次の目的地をセット。次のtickでドッキングを解除、バリデーションが行われる
                    trainDiagram.Update();
                    if (trainDiagram.CanCurrentEntryDepart())
                    {
                        // 次の目的地をセット
                        DepartFromCurrentEntry();
                    }
                    return 0;
                }
                else
                {
                    // ドッキング中でなければ目的地に向かって進む
                    UpdateMasconLevel();
                }
            }
            else 
            {
                // もしドッキング中なら
                if (trainUnitStationDocking.IsDocked)
                {
                    // 強制ドッキング解除
                    trainUnitStationDocking.UndockFromStation();
                    return 0;
                }
                else
                {
                    // ドッキング中でなければキー操作で目的地 or nullに向かって進む
                    KeyInput();
                }
            }

            // マスコンレベルから燃料を消費しつつ速度を計算する
            // マスコン確定後に進む距離を算出
            // Calculate distance to travel after mascon decision
            var distanceToMove = SimulateMotionStep();
            return UpdateTrainByDistance(distanceToMove);
        }

        //キー操作系
        public void KeyInput() 
        {
            masconLevel = 0;
            //wキーでmasconLevel=16777216
            //sキーでmasconLevel=-16777216
        }

        private void DepartFromCurrentEntry()
        {
            if (trainUnitStationDocking.IsDocked)
            {
                trainUnitStationDocking.UndockFromStation();
            }
            trainDiagram.TryMoveToNextEntryAndNotifyDeparted(_trainUpdateService.GetCurrentTick());
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


        // Updateの距離int版
        // distanceToMoveの距離絶対進む。進んだ距離を返す
        // 目的地は常にtrainDiagram.GetNextDestination()を見るので最新のtrainDiagramが適応される。もし目的地がnullなら_isAutoRun = false;は上記ループで行われる(なぜなら1フレーム対応が遅れるので)
        public int UpdateTrainByDistance(int distanceToMove) 
        {
            //進行メインループ
            int totalMoved = 0;
            //何かが原因で無限ループになることがあるので、一定回数で強制終了する
            int loopCount = 0;
            while (true)
            {
                int moveLength = _railPosition.MoveForward(distanceToMove);
                distanceToMove -= moveLength;
                totalMoved += moveLength;
                _remainingDistance -= moveLength;
                //自動運転で目的地に到着してたらドッキング判定を行う必要がある
                if (_railPosition.DistanceToNextNode == 0)
                {
                    if (IsArrivedDestination() && _isAutoRun)
                    {
                        _currentSpeed = 0;
                        _accumulatedDistance = 0;
                        //diagramが駅を見ている場合
                        if (trainDiagram.GetCurrentNode().StationRef.StationBlock != null)
                        {
                            var wasDocked = trainUnitStationDocking.IsDocked;
                            trainUnitStationDocking.TryDockWhenStopped();
                            // この瞬間ドッキングしたらDockedイベントのみ通知する
                            // Notify Docked only when transition to docked is observed.
                            if (trainUnitStationDocking.IsDocked)
                            {
                                if (!wasDocked)
                                {
                                    trainDiagram.NotifyDocked(_trainUpdateService.GetCurrentTick());
                                }
                            }
                        }
                        else//diagramが非駅を見ている場合 
                        {
                            // 次の目的地をセット
                            trainDiagram.TryAdvanceToNextEntryFromDeparture();
                        }
                        break;
                    }
                }
                if (distanceToMove == 0) break;
                //----------------------------------------------------------------------------------------
                //この時点でdistanceToMoveが0以外かつ分岐地点または行き止まりについてる状況
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
                        UnityEngine.Debug.Log("diagramの登録nodeに対する経路が全てなくなった。自動運転off");
                        TurnOffAutoRun();
                        break;
                    }
                    //見つかったので一番いいルートを自動選択
                    _railPosition.AddNodeToHead(newPath[1]);//newPath[0]はapproachingがはいってる
                                                            //残りの距離を再更新
                    _remainingDistance = RailNodeCalculate.CalculateTotalDistanceF(newPath);//計算量NlogN(logはnodeからintの辞書アクセス)
                }
                else
                {
                    //approachingから次のノードをリストの若い順に取得して_railPosition.AddNodeToHead
                    var nextNodelist = approaching.ConnectedNodes.ToList();
                    if (nextNodelist.Count == 0)
                    {
                        _currentSpeed = 0;
                        break;//もう進めない
                    }
                    var nextNode = nextNodelist[0];
                    _railPosition.AddNodeToHead(nextNode);
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


        //毎フレーム燃料の在庫を確認しながら加速力を計算する
        public double UpdateTractionForce(int masconLevel)
        {
            int totalWeight = 0;
            int totalTraction = 0;

            foreach (var car in _cars)
            {
                var (weight, traction) = car.GetWeightAndTraction();//forceに応じて燃料が消費される:未実装
                totalWeight += weight;
                totalTraction += traction;
            }
            return (double)totalTraction / totalWeight * masconLevel / TrainMotionParameters.MasconLevelMaximum;
        }

        //diagramのindexが見ている目的地にちょうど0距離で到達したか
        private bool IsArrivedDestination()
        {
            var node = _railPosition.GetNodeApproaching();
            var dnode = trainDiagram.GetCurrentNode();
            if (dnode == null) return false;
            if ((node.NodeGuid == trainDiagram.GetCurrentNode().NodeGuid) && (_railPosition.GetDistanceToNextNode() == 0))
            {
                return true;
            }
            return false;
        }

        
        public void TurnOnAutoRun()
        {
            //バリデーションでoff条件をあらいだし
            _isAutoRun = true;
            DiagramValidation();
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
            //目的地にすでに到達している場合、remainingdistanceを更新
            if (approaching == destinationNode)
            {
                _remainingDistance = _railPosition.GetDistanceToNextNode();
                return;
            }
            var (found, newPath) = CheckAllDiagramPath(approaching);
            if (!found)//全部の経路がなくなった
            {
                UnityEngine.Debug.Log("diagramの登録nodeに対する経路が全てない。自動運転off");
                TurnOffAutoRun();
                return;
            }
            //_remainingDistance更新
            _remainingDistance = _railPosition.GetDistanceToNextNode() + RailNodeCalculate.CalculateTotalDistanceF(newPath);
        }

        public void TurnOffAutoRun()
        {
            _isAutoRun = false;
            _remainingDistance = int.MaxValue;
            masconLevel = 0;
            _accumulatedDistance = 0;
            //ドッキングしていたら解除する
            if (trainUnitStationDocking.IsDocked)
            {
                trainUnitStationDocking.UndockFromStation();
            }
        }

        //現在のdiagramのcurrentから順にすべてのエントリーを順番にみていって、approachingからエントリーnodeへpathが繋がっていればtrueを返す
        public (bool, List<IRailNode>) CheckAllDiagramPath(IRailNode approaching) 
        {
            IRailNode destinationNode = null;
            List<IRailNode> newPath = null;
            //ダイアグラム上、次に目的地に変更していく。全部の経路がなくなった場合は自動運転を解除する
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
                //見つかったのでループを抜ける
                found = true;
                break;
            }
            return (found, newPath);
        }

        //列車編成を保存する。ブロックとは違うことに注意
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

            var trainUpdateService = ServerContext.GetService<TrainUpdateService>();
            var railPositionManager = ServerContext.GetService<TrainRailPositionManager>();
            var diagramManager = ServerContext.GetService<TrainDiagramManager>();

            var trainUnit = new TrainUnit(railPosition, cars, trainUpdateService, railPositionManager, diagramManager, false)
            {
                _isAutoRun = saveData.IsAutoRun,
                _currentSpeed = restoredSpeed,
                _accumulatedDistance = restoredAccumulatedDistance
            };

            trainUnit._remainingDistance = trainUnit._railPosition.GetDistanceToNextNode();
            trainUnit.trainDiagram.RestoreState(saveData.Diagram);

            if (trainUnit._isAutoRun)
            {
                trainUnit.DiagramValidation();
            }
            return trainUnit;
        }

        //============================================================
        // ▼ ここからが「編成を分割連結する」ための処理例
        //============================================================
        /// <summary>
        ///  分割
        ///  列車を「後ろから numberOfCarsToDetach 両」切り離して、後ろの部分を新しいTrainUnitとして返す
        ///  新しいTrainUnitのrailpositionは、切り離した車両の長さに応じて調整される
        ///  新しいTrainUnitのtrainDiagramは空になる
        ///  新しいTrainUnitのドッキング状態はcarに情報があるためそのまま保存される
        /// </summary>

        public TrainUnit SplitTrain(int numberOfCarsToDetach)
        {
            // 例：10両 → 5両 + 5両など
            // 後ろから 5両を抜き取るケースを想定
            if (numberOfCarsToDetach <= 0 || numberOfCarsToDetach > _cars.Count)
            {
                if (numberOfCarsToDetach != 0)
                    UnityEngine.Debug.LogWarning("SplitTrain: 指定両数が不正です。");
                return null;
            }
            TurnOffAutoRun();
            // 1) 切り離す車両リストを作成
            //    後ろ側から numberOfCarsToDetach 両を取得
            var detachedCars = _cars
                .Skip(_cars.Count - numberOfCarsToDetach)
                .ToList();
            // 3) 新しく後ろのTrainUnitを作る
            var splittedRailPosition = CreateSplittedRailPosition(detachedCars);
            // 2) 既存のTrainUnitからは そのぶん削除
            _cars.RemoveRange(_cars.Count - numberOfCarsToDetach, numberOfCarsToDetach);
            // _carsの両数に応じて、列車長を算出する
            int newTrainLength = 0;
            foreach (var car in _cars)
                newTrainLength += car.Length;
            _railPosition.SetTrainLength(newTrainLength);
            // 4) 新しいTrainUnitを作成
            var splittedUnit = new TrainUnit(splittedRailPosition, detachedCars, _trainUpdateService, _railPositionManager, _diagramManager, false);
            // 5) 自分が0になっていたら
            if (_cars.Count == 0)
                this.OnDestroy();
            // 6) 新しいTrainUnitを返す
            return splittedUnit;

            #region Internal
            /// <summary>
            /// 後続列車のために、新しいRailPositionを生成し返す。
            /// ここでは単純に列車の先頭からRailNodeの距離を調整するだけ
            /// </summary>
            RailPosition CreateSplittedRailPosition(List<TrainCar> splittedCars)
            {
                // _railPositionのdeepコピー
                var newNodes = _railPosition.DeepCopy();
                // splittedCarsの両数に応じて、列車長を算出する
                int splittedTrainLength = 0;
                foreach (var car in splittedCars)
                    splittedTrainLength += car.Length;
                // 切り離した長さが列車の全長と一致した時点で、現状のまま戻す
                // Return as-is when the detached length matches the full train length
                if (splittedTrainLength == newNodes.TrainLength) return newNodes;
                //newNodesを反転して、新しい列車長を設定
                newNodes.Reverse();
                newNodes.SetTrainLength(splittedTrainLength);
                //また反転すればちゃんと後ろの列車になる
                newNodes.Reverse();
                return newNodes;
            }
            #endregion
        }

        /// <summary>
        /// 指定 GUID or indexの列車両を安全に削除する
        /// 削除後分割された新編成は返さない(必要があれば返すこともできる)
        /// Removes the train car that matches the given GUID.
        /// The newly split configuration after deletion will not be returned (it can be returned if necessary).
        /// </summary>
        /// 
        public void RemoveCar(int targetIndex)
        {
            if ((targetIndex < 0) || (targetIndex >= _cars.Count))
            {
                UnityEngine.Debug.LogWarning($"RemoveCar: carIndex {targetIndex} is not found.");
                return;
            }
            var carsBehind = _cars.Count - targetIndex - 1;
            SplitTrain(carsBehind);
            var removeCar = SplitTrain(1);
            removeCar?.OnDestroy();
        }
        public void RemoveCar(TrainCarInstanceId trainCarInstanceId)
        {
            var targetIndex = _cars.FindIndex(car => car.TrainCarInstanceId == trainCarInstanceId);
            RemoveCar(targetIndex);
        }

        /// <summary>
        /// 列車編成の先頭or最後尾に1両連結する
        /// 現時点での実装は、RailPosition railPositionはこの追加Carの前輪～後輪までの範囲をもったrailPositionが必要かつ
        /// そのどちらかは現時点のTrainUnitの先頭or最後尾に接続されている必要がある
        /// </summary>
        public void AttachCarToHead(TrainCar car, RailPosition railPosition)
        {
            //trainUnitの先頭にcarが連結するのでcarのrailPositionはcarの前輪～trainUnitの先頭前輪までを指してないといけない
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
            // RailPositionを更新(内部で自動で追加する車両分の距離を伸ばす)
            _railPosition.AppendRailPositionAtRear(railPosition);
        }

        public void OnTrainDocked()
        {
        }

        public void OnTrainUndocked()
        {
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
            
            _trainUpdateService.UnregisterTrain(this);

            trainDiagram = null;
            _railPosition = null;
            trainUnitStationDocking = null;
            _cars = null;
            _trainId = Guid.Empty;
        }
    }
}


