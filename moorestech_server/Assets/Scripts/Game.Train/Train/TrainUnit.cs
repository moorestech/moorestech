using Core.Item.Interface;
using Game.Context;
using Game.Train.Common;
using Game.Train.RailGraph;
using Game.Train.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;

namespace Game.Train.Train
{
    /// <summary>
    /// 複数車両からなる列車編成全体を表すクラス
    /// Represents an entire train formation composed of multiple cars.
    /// </summary>
    public class TrainUnit
    {
        public string SaveKey { get; } = typeof(TrainUnit).FullName;
        
        private RailPosition _railPosition;
        private Guid _trainId;
        public Guid TrainId => _trainId;

        private int _remainingDistance;// 自動減速用
        private bool _isAutoRun;
        public bool IsAutoRun
        {
            get { return _isAutoRun; }
        }
        //autorun時の1tick前のentryのguid
        private Guid _previousEntryGuid;

        private double _currentSpeed;   // m/s など適宜
        public double CurrentSpeed => _currentSpeed;
        private double _accumulatedDistance; // 累積距離、距離の小数点以下を保持するために使用
        //摩擦係数、空気抵抗係数などはここに追加する
        private const double FRICTION = 0.0002;
        private const double AIR_RESISTANCE = 0.00002;
        private const double SpeedWeight = 0.008;//1tickで_currentSpeedが進む距離に変換されるための重み付け係数
        private const double AutoRunMaxSpeedDistanceCoefficient = 10000.0;//自動運転の最大速度計算用距離係数
        private const double AutoRunMaxSpeedOffset = 10.0;//自動運転で、AutoRunMaxSpeedOffsetは目的距離が近すぎても進めるようにするためのバッファ
        private const double AutoRunSpeedBufferMargin = 0.02;
        private const double AutoRunSpeedBufferRate = 1.0 - AutoRunSpeedBufferMargin;
        private const double TractionForceAccelerationRate = 0.1;
        private const double ManualControlDecelerationFactor = 1.0;//マスコンレベル手動調整用
        private const int MasconLevelMaximum = 16777216;//電車でGOでP5-B8まであるやつ
        private const int InfiniteLoopGuardThreshold = 1_000_000;

        private List<TrainCar> _cars;
        public RailPosition RailPosition => _railPosition;
        public IReadOnlyList<TrainCar> Cars => _cars;
        public TrainUnitStationDocking trainUnitStationDocking; // 列車の駅ドッキング用のクラス
        public TrainDiagram trainDiagram; // 列車のダイアグラム
        //キー関連
        //マスコンレベル 0がニュートラル、1が前進1段階、-1が後退1段階.キー入力やテスト、外部から直接制御できる。min maxは±16777216とする(暫定)
        public int masconLevel = 0;
        private int tickCounter = 0;// TODO デバッグトグル関係　そのうち消す
        public TrainUnit(
            RailPosition initialPosition,
            List<TrainCar> cars
        )
        {
            _railPosition = initialPosition;
            TrainRailPositionManager.Instance.RegisterRailPosition(_railPosition);
            _trainId = Guid.NewGuid();
            _cars = cars;
            _currentSpeed = 0.0; // 仮の初期速度
            _isAutoRun = false;
            _previousEntryGuid = Guid.Empty;
            trainUnitStationDocking = new TrainUnitStationDocking(this);
            trainDiagram = new TrainDiagram();
            TrainUpdateService.Instance.RegisterTrain(this);
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
            //数十回に1回くらいの頻度でデバッグログを出す
            tickCounter++;
            if (TrainUpdateService.TrainAutoRunDebugEnabled && tickCounter % 20 == 0)
                UnityEngine.Debug.Log("spd="+_currentSpeed + "_Auto=" + IsAutoRun + "_DiagramCount" + trainDiagram.Entries.Count);// TODO デバッグトグル関係　そのうち消す

            if (IsAutoRun)
            {
                //まずdiagramの変更有無を確認する
                // 自動運転中に手動でダイアグラムをいじって目的地がnullになった場合は自動運転を解除する
                if (trainDiagram.GetCurrentNode() == null)
                {
                    UnityEngine.Debug.Log("自動運転中に手動でダイアグラムをいじって目的地がnullになったので自動運転を解除");
                    TurnOffAutoRun();
                    _currentSpeed = 0;
                    return 0;
                }
                //diagramを手動でいじって、現在ドッキング中の駅をエントリーから削除したときなど。その場合は安全にドッキング解除しtrainDiagram.MoveToNextEntry();はしない
                if (_previousEntryGuid != trainDiagram.GetCurrentGuid())
                {
                    if (trainUnitStationDocking.IsDocked)
                    {
                        trainUnitStationDocking.UndockFromStation();
                        UnityEngine.Debug.Log("diagram変更検知によるドッキング解除");
                    }
                    DiagramValidation();
                }

                _previousEntryGuid = trainDiagram.GetCurrentGuid();

                // 自動運転中はドッキング中なら進まない、ドッキング中じゃないなら目的地に向かって加速
                if (trainUnitStationDocking.IsDocked)
                {
                    _currentSpeed = 0;
                    if (TrainUpdateService.TrainAutoRunDebugEnabled && tickCounter % 20 == 0)
                        UnityEngine.Debug.Log("ドッキング中");// TODO デバッグトグル関係　そのうち消す
                    trainUnitStationDocking.TickDockedStations();
                    // もしtrainDiagramの出発条件を満たしていたら、trainDiagramは次の目的地をセット。次のtickでドッキングを解除、バリデーションが行われる
                    if (trainDiagram.CheckEntries(this))
                    {
                        // ドッキングを解除はGuid違いの検出により次のtickで行う
                        //trainUnitStationDocking.UndockFromStation();
                        // 次の目的地をセット
                        _previousEntryGuid = Guid.Empty;//同じentryに戻るときを考慮。別entryにいくものとして扱う
                        trainDiagram.MoveToNextEntry();
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
                _previousEntryGuid = Guid.Empty;
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

            //マスコンレベルから燃料を消費しつつ速度を計算する
            UpdateTrainSpeed();
            //距離計算 進むor後進する
            double floatDistance = _currentSpeed * SpeedWeight;
            _accumulatedDistance += floatDistance;
            int distance = (int)Math.Truncate(_accumulatedDistance);
            _accumulatedDistance -= distance;
            return UpdateTrainByDistance(distance);
        }

        //キー操作系
        public void KeyInput() 
        {
            masconLevel = 0;
            //wキーでmasconLevel=16777216
            //sキーでmasconLevel=-16777216
        }

        // AutoRun時に目的地に向かうためのマスコンレベル更新
        public void UpdateMasconLevel()
        {
            masconLevel = 0;
            //目的地に近ければ減速したい。自動運行での最大速度を決めておく
            double maxspeed = Math.Sqrt(((double)_remainingDistance) * AutoRunMaxSpeedDistanceCoefficient) + AutoRunMaxSpeedOffset;//AutoRunMaxSpeedOffsetは距離が近すぎても進めるようにするためのバッファ
            //全力加速する必要がある。マスコンレベルmax
            if (maxspeed > _currentSpeed)
            {
                masconLevel = MasconLevelMaximum;
            }
            //
            if (maxspeed < _currentSpeed * AutoRunSpeedBufferRate)//AutoRunSpeedBufferMarginぶんはバッファ
            {
                var bufferAdjustedSpeed = _currentSpeed * AutoRunSpeedBufferRate;
                var subspeed = maxspeed - bufferAdjustedSpeed;
                masconLevel = Math.Max((int)subspeed, -MasconLevelMaximum);
            }
        }

        // 速度更新、自動時、手動時両方
        // 進むべき距離を返す
        public void UpdateTrainSpeed() 
        {
            double force = 0.0;
            int sign, sign2;
            //マスコン操作での加減速
            if (masconLevel > 0)
            {
                force = UpdateTractionForce(masconLevel);
                _currentSpeed += force * TractionForceAccelerationRate;
            }
            else
            {
                //currentspeedがマイナスも考慮
                sign = Math.Sign(_currentSpeed);
                _currentSpeed += sign * masconLevel * ManualControlDecelerationFactor; // ManualControlDecelerationFactorは調整用定数
                sign2 = Math.Sign(_currentSpeed);
                if (sign != sign2) _currentSpeed = 0; // 逆方向に行かないようにする
            }

            //どちらにしても速度は摩擦で減少する。摩擦は速度の1乗、空気抵抗は速度の2乗に比例するとする
            force = Math.Abs(_currentSpeed) * SpeedWeight * FRICTION + _currentSpeed * _currentSpeed * SpeedWeight * AIR_RESISTANCE;
            sign = Math.Sign(_currentSpeed);
            _currentSpeed -= sign * force;
            sign2 = Math.Sign(_currentSpeed);
            if (sign != sign2) _currentSpeed = 0; // 逆方向に行かないようにする
            return;
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
                if (IsArrivedDestination() && _isAutoRun)
                {
                    _currentSpeed = 0;
                    _accumulatedDistance = 0;
                    //diagramが駅を見ている場合
                    if (trainDiagram.GetCurrentNode().StationRef.StationBlock != null)
                    {
                        trainUnitStationDocking.TryDockWhenStopped();
                        //この瞬間ドッキングしたら、diagramの出発条件リセット
                        if (trainUnitStationDocking.IsDocked) 
                        {
                            trainDiagram.ResetCurrentEntryDepartureConditions();
                        }
                    }
                    else//diagramが非駅を見ている場合 
                    {
                        // 次の目的地をセット
                        _previousEntryGuid = Guid.Empty;//同じentryに戻るときを考慮。別entryにいくものとして扱う
                        trainDiagram.MoveToNextEntry();
                    }
                    break;
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
                    _previousEntryGuid = trainDiagram.GetCurrentGuid();
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
            return (double)totalTraction / totalWeight * masconLevel / MasconLevelMaximum;
        }

        //diagramのindexが見ている目的地にちょうど0距離で到達したか
        private bool IsArrivedDestination()
        {
            var node = _railPosition.GetNodeApproaching();
            if ((node == trainDiagram.GetCurrentNode()) & (_railPosition.GetDistanceToNextNode() == 0))
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
            _previousEntryGuid = Guid.Empty;
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
                newPath = RailGraphDatastore.FindShortestPath(approaching, destinationNode);
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
            var railSnapshot = _railPosition != null
                ? new List<ConnectionDestination>(_railPosition.CreateSaveSnapshot())
                : new List<ConnectionDestination>();

            var carStates = _cars != null
                ? _cars.Select(CreateTrainCarSaveData).ToList()
                : new List<TrainCarSaveData>();

            var diagramState = trainDiagram != null
                ? CreateTrainDiagramSaveData(trainDiagram)
                : null;

            return new TrainUnitSaveData
            {
                TrainLength = _railPosition?.TrainLength ?? 0,
                DistanceToNextNode = _railPosition?.DistanceToNextNode ?? 0,
                RailSnapshot = railSnapshot,
                IsAutoRun = _isAutoRun,
                PreviousEntryGuid = _previousEntryGuid,
                CurrentSpeedBits = BitConverter.DoubleToInt64Bits(_currentSpeed),
                AccumulatedDistanceBits = BitConverter.DoubleToInt64Bits(_accumulatedDistance),
                Cars = carStates,
                Diagram = diagramState
            };
        }

        private static TrainCarSaveData CreateTrainCarSaveData(TrainCar car)
        {
            var inventoryItems = new List<ItemStackSaveJsonObject>(car.InventorySlots);
            for (int i = 0; i < car.InventorySlots; i++)
            {
                inventoryItems.Add(new ItemStackSaveJsonObject(car.GetItem(i)));
            }

            var fuelItems = new List<ItemStackSaveJsonObject>(car.FuelSlots);
            for (int i = 0; i < car.FuelSlots; i++)
            {
                fuelItems.Add(new ItemStackSaveJsonObject(car.GetFuelItem(i)));
            }

            SerializableVector3Int? dockingPosition = null;
            if (car.dockingblock != null)
            {
                var blockPosition = car.dockingblock.BlockPositionInfo.OriginalPos;
                dockingPosition = new SerializableVector3Int(blockPosition.x, blockPosition.y, blockPosition.z);
            }

            return new TrainCarSaveData
            {
                TrainCarGuid = car.TrainCarMasterElement.TrainCarGuid,
                IsFacingForward = car.IsFacingForward,
                DockingBlockPosition = dockingPosition,
                InventoryItems = inventoryItems,
                FuelItems = fuelItems
            };
        }

        private static TrainDiagramSaveData CreateTrainDiagramSaveData(TrainDiagram diagram)
        {
            var entries = new List<TrainDiagramEntrySaveData>();
            foreach (var entry in diagram.Entries)
            {
                entries.Add(new TrainDiagramEntrySaveData
                {
                    EntryId = entry.entryId,
                    Node = CreateConnectionDestinationSnapshot(entry.Node),
                    DepartureConditions = entry.DepartureConditionTypes?.ToList() ?? new List<TrainDiagram.DepartureConditionType>(),
                    WaitForTicksInitial = entry.GetWaitForTicksInitialTicks(),
                    WaitForTicksRemaining = entry.GetWaitForTicksRemainingTicks()
                });
            }

            return new TrainDiagramSaveData
            {
                CurrentIndex = diagram.CurrentIndex,
                Entries = entries
            };
        }

        private static ConnectionDestination CreateConnectionDestinationSnapshot(IRailNode node)
        {
            if (node == null)
                return ConnectionDestination.Default;
            return node.ConnectionDestination;
        }

        private static List<IRailNode> RestoreRailNodes(IEnumerable<ConnectionDestination> snapshot)
        {
            var nodes = new List<IRailNode>();
            if (snapshot == null)
            {
                return nodes;
            }

            foreach (var destination in snapshot)
            {
                var node = RailGraphDatastore.ResolveRailNode(destination);
                if (node != null)
                {
                    nodes.Add(node);
                }
            }
            return nodes;
        }
        //別コードに分割したい TODO
        private static List<TrainCar> RestoreTrainCars(List<TrainCarSaveData> carData)
        {
            var cars = new List<TrainCar>();
            if (carData == null)
            {
                return cars;
            }

            foreach (var data in carData)
            {
                var car = RestoreTrainCar(data);
                if (car != null)
                {
                    cars.Add(car);
                }
            }

            return cars;
        }

        private static TrainCar RestoreTrainCar(TrainCarSaveData data)
        {
            if (data == null)
            {
                return null;
            }
            
            if (!MasterHolder.TrainUnitMaster.TryGetTrainUnit(data.TrainCarGuid, out var trainCarMaster)) throw new Exception("trainCarMaster is not found");
            var isFacingForward = data.IsFacingForward;
            var car = new TrainCar(trainCarMaster, isFacingForward);

            var empty = ServerContext.ItemStackFactory.CreatEmpty();

            for (int i = 0; i < car.GetSlotSize(); i++)
            {
                IItemStack item = empty;
                if (data.InventoryItems != null && i < data.InventoryItems.Count)
                {
                    item = data.InventoryItems[i]?.ToItemStack() ?? empty;
                }
                car.SetItem(i, item);
            }

            for (int i = 0; i < car.FuelSlots; i++)
            {
                IItemStack item = empty;
                if (data.FuelItems != null && i < data.FuelItems.Count)
                {
                    item = data.FuelItems[i]?.ToItemStack() ?? empty;
                }
                car.SetFuelItem(i, item);
            }

            if (data.DockingBlockPosition.HasValue)
            {
                var block = ServerContext.WorldBlockDatastore.GetBlock((UnityEngine.Vector3Int)data.DockingBlockPosition.Value);
                if (block != null)
                {
                    car.dockingblock = block;
                }
            }

            return car;
        }

        private static void RestoreTrainDiagram(TrainDiagram diagram, TrainDiagramSaveData saveData)
        {
            if (diagram == null || saveData == null)
            {
                return;
            }
            diagram.RestoreState(saveData);
        }

        public static TrainUnit RestoreFromSaveData(TrainUnitSaveData saveData)
        {
            if (saveData == null)
            {
                return null;
            }

            var nodes = RestoreRailNodes(saveData.RailSnapshot);
            if (nodes.Count == 0)
            {
                return null;
            }

            var trainLength = saveData.TrainLength;
            if (trainLength < 0)
            {
                trainLength = 0;
            }

            var distanceToNextNode = saveData.DistanceToNextNode;
            if (distanceToNextNode < 0)
            {
                distanceToNextNode = 0;
            }

            var railPosition = new RailPosition(nodes, trainLength, distanceToNextNode);
            var cars = RestoreTrainCars(saveData.Cars);

            var restoredSpeed = saveData.CurrentSpeedBits.HasValue
                ? BitConverter.Int64BitsToDouble(saveData.CurrentSpeedBits.Value)
                : 0;
            var restoredAccumulatedDistance = saveData.AccumulatedDistanceBits.HasValue
                ? BitConverter.Int64BitsToDouble(saveData.AccumulatedDistanceBits.Value)
                : 0;

            var trainUnit = new TrainUnit(railPosition, cars)
            {
                _isAutoRun = saveData.IsAutoRun,
                _previousEntryGuid = saveData.PreviousEntryGuid,
                _currentSpeed = restoredSpeed,
                _accumulatedDistance = restoredAccumulatedDistance
            };

            trainUnit._remainingDistance = trainUnit._railPosition.GetDistanceToNextNode();

            RestoreTrainDiagram(trainUnit.trainDiagram, saveData.Diagram);

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
                UnityEngine.Debug.LogError("SplitTrain: 指定両数が不正です。");
                return null;
            }
            if (numberOfCarsToDetach == _cars.Count) 
            {
                OnDestroy();
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
            var splittedUnit = new TrainUnit(
                splittedRailPosition,
                detachedCars
            );
            // 6) 新しいTrainUnitを返す
            return splittedUnit;
        }

        /// <summary>
        /// 後続列車のために、新しいRailPositionを生成し返す。
        /// ここでは単純に列車の先頭からRailNodeの距離を調整するだけ
        /// </summary>
        private RailPosition CreateSplittedRailPosition(List<TrainCar> splittedCars)
        {
            // _railPositionのdeepコピー
            var newNodes = _railPosition.DeepCopy();
            // splittedCarsの両数に応じて、列車長を算出する
            int splittedTrainLength = 0;
            foreach (var car in splittedCars)
                splittedTrainLength += car.Length;
            //newNodesを反転して、新しい列車長を設定
            newNodes.Reverse();
            newNodes.SetTrainLength(splittedTrainLength);
            //また反転すればちゃんと後ろの列車になる
            newNodes.Reverse();
            return newNodes;
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

        public void OnDestroy()
        {
            TrainRailPositionManager.Instance.UnregisterRailPosition(_railPosition);
            trainDiagram.OnDestroy();
            _railPosition.OnDestroy();
            trainUnitStationDocking.OnDestroy();

            foreach (var car in _cars)
            {
                car.Destroy();
            }
            _cars.Clear();
            
            TrainUpdateService.Instance.UnregisterTrain(this);

            trainDiagram = null;
            _railPosition = null;
            trainUnitStationDocking = null;
            _cars = null;
            _trainId = Guid.Empty;
        }
    }

    [Serializable]
    public class TrainUnitSaveData
    {
        public int TrainLength { get; set; }
        public int DistanceToNextNode { get; set; }
        public List<ConnectionDestination> RailSnapshot { get; set; }
        public bool IsAutoRun { get; set; }
        public Guid PreviousEntryGuid { get; set; }
        public long? CurrentSpeedBits { get; set; }
        public long? AccumulatedDistanceBits { get; set; }
        public List<TrainCarSaveData> Cars { get; set; }
        public TrainDiagramSaveData Diagram { get; set; }
    }

    [Serializable]
    public class TrainCarSaveData
    {
        public Guid TrainCarGuid { get; set; }
        public bool IsFacingForward { get; set; }
        public SerializableVector3Int? DockingBlockPosition { get; set; }
        public List<ItemStackSaveJsonObject> InventoryItems { get; set; }
        public List<ItemStackSaveJsonObject> FuelItems { get; set; }
    }

    [Serializable]
    public class TrainDiagramSaveData
    {
        public int CurrentIndex { get; set; }
        public List<TrainDiagramEntrySaveData> Entries { get; set; }
    }

    [Serializable]
    public class TrainDiagramEntrySaveData
    {
        public Guid EntryId { get; set; }
        public ConnectionDestination Node { get; set; }
        public List<TrainDiagram.DepartureConditionType> DepartureConditions { get; set; }
        public int? WaitForTicksInitial { get; set; }
        public int? WaitForTicksRemaining { get; set; }
    }

}
