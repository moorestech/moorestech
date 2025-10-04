using System;
using System.Collections.Generic;
using System.Linq;
using Game.Train.Common;
using Game.Train.RailGraph;
using Game.Train.Utility;

namespace Game.Train.Train
{
    public class TrainUnit
    {
        public string SaveKey { get; } = typeof(TrainUnit).FullName;
        
        public RailPosition _railPosition;
        public RailNode _destinationNode; // 現在の最終目的地（駅ノードなど）
        // _destinationNodeまでの距離
        private readonly Guid _trainId;
        private int _remainingDistance;
        private bool _isAutoRun;
        public bool IsAutoRun
        {
            get { return _isAutoRun; }
        }

        public Guid TrainId => _trainId;

        public double _currentSpeed;   // m/s など適宜
        //摩擦係数、空気抵抗係数などはここに追加する
        const double FRICTION = 0.0002f;
        const double AIR_RESISTANCE = 0.00002f;

        public List<TrainCar> _cars;
        public TrainUnitStationDocking trainUnitStationDocking; // 列車の駅ドッキング用のクラス
        public TrainDiagram trainDiagram; // 列車のダイアグラム


        public TrainUnit(
            RailPosition initialPosition,
            List<TrainCar> cars,
            RailNode destination = null
        )
        {
            _railPosition = initialPosition;
            _trainId = Guid.NewGuid();
            _destinationNode = destination;
            _cars = cars;  // 追加
            _currentSpeed = 0.0; // 仮の初期速度
            _isAutoRun = false;
            trainUnitStationDocking = new TrainUnitStationDocking(this);
            trainDiagram = new TrainDiagram(this);

            TrainDiagramManager.Instance.RegisterDiagram(this, trainDiagram);
            TrainUpdateService.Instance.RegisterTrain(this);
        }


        public void Update(double deltaTime) 
        {
            if (IsAutoRun)
            {
                // 自動運転中はドッキング中なら進まない、ドッキング中じゃないなら目的地に向かって加速
                if (trainUnitStationDocking.IsDocked)
                {
                    trainUnitStationDocking.TickDockedStations();
                    // ドッキング中は進まない
                    _currentSpeed = 0;
                    // もしtrainDiagramの出発条件を満たしていたらtrainDiagramは次の目的地をセットして、ドッキングを解除する
                    if (trainDiagram.CheckEntries())
                    {
                        // 次の目的地をセット
                        trainDiagram.MoveToNextEntry();
                        _destinationNode = trainDiagram.GetNextDestination();
                        // ドッキングを解除
                        trainUnitStationDocking.UndockFromStation();
                    }
                }
                else
                {
                    // ドッキング中でなければ目的地に向かって進む
                    _destinationNode = trainDiagram.GetNextDestination();//これは手動でRailNodeが消されたときに毎フレーム"必ず存在する"目的地を見るため
                    UpdateTrainByTime(deltaTime);
                }
            }
            else 
            {
                // TODO 手動運転中はFキーとかでドッキングできる(satisfactoryを参考に)
                // 未実装
                // もしドッキング中なら
                if (trainUnitStationDocking.IsDocked)
                {
                    trainUnitStationDocking.TickDockedStations();
                    // ドッキング中は進まない
                    _currentSpeed = 0;
                    // Fキーとかでドッキング解除
                    // trainUnitStationDocking.TurnOffDockingStates();
                }
                else
                {
                    // ドッキング中でなければキー操作で目的地に向かって進む
                    _destinationNode = trainDiagram.GetNextDestination();
                    UpdateTrainByTime(deltaTime);
                }
            }
                
        }

        // Updateの時間版
        // 進んだ距離を返す
        public int UpdateTrainByTime(double deltaTime) 
        {
            //目的地に近ければ減速したい。自動運行での最大速度を決めておく
            double maxspeed = Math.Sqrt(((double)_remainingDistance) * 10000.0) + 10.0;//10.0は距離が近すぎても進めるよう
            if (_isAutoRun)//設定している目的地に向かうべきなら
            {
                //加速する必要がある
                if (maxspeed > _currentSpeed)
                {
                    var force = UpdateTractionForce(deltaTime);
                    _currentSpeed += force * deltaTime;
                }
            }
            //どちらにしても速度は摩擦で減少する。摩擦は速度の1乗、空気抵抗は速度の2乗に比例するとする
            //deltaTime次第でかわる
            _currentSpeed -= _currentSpeed * deltaTime * FRICTION + _currentSpeed * _currentSpeed * deltaTime * AIR_RESISTANCE;
            //速度が0以下にならないようにする
            _currentSpeed = Math.Max(0, _currentSpeed);
            //maxspeed制約
            _currentSpeed = Math.Min(maxspeed, _currentSpeed);

            double floatDistance = _currentSpeed * deltaTime;
            //floatDistanceが1.5ならランダムで1か2になる
            //floatDistanceが-1.5ならランダムで-1か-2になる
            int distanceToMove = UnityEngine.Mathf.FloorToInt((float)floatDistance + UnityEngine.Random.Range(0f, 0.999f));
            UpdateTrainByDistance(distanceToMove);
            return distanceToMove;
        }

        // Updateの距離int版
        // distanceToMoveの距離絶対進むが、唯一目的地についたときだけ残りの距離を返す
        public int UpdateTrainByDistance(int distanceToMove) 
        {
            //進行メインループ
            //何かが原因で無限ループになることがあるので、一定回数で強制終了する
            int loopCount = 0;
            while (true)
            {
                int moveLength = _railPosition.MoveForward(distanceToMove);
                distanceToMove -= moveLength;
                _remainingDistance -= moveLength;
                //自動運転で目的地に到着してたらドッキング判定を行う必要がある
                if (IsArrivedDestination() && _isAutoRun)
                {
                    _currentSpeed = 0;
                    trainUnitStationDocking.TryDockWhenStopped();
                    break;
                }
                if (distanceToMove == 0) break;
                //----------------------------------------------------------------------------------------
                //この時点でdistanceToMoveが0以外かつ分岐地点または行き止まりについてる状況
                RailNode approaching = _railPosition.GetNodeApproaching();
                if (approaching == null) 
                {
                    _isAutoRun = false;
                    _currentSpeed = 0;
                    throw new InvalidOperationException("列車が進行中に接近しているノードがnullになりました。");
                }
                
                //ランダム経路選択をするか否か
                bool isRandomPathUse = true;
                if (_destinationNode != null)//自動運転なら必ず!=nullだし手動運転でも!=nullなら自動で経路検索していいだろう
                {
                    //分岐点で必ず最短経路を再度探す。手動でレールが変更されてるかもしれないので
                    //最低でも返りlistにはapproaching, _destinationNodeが入っているはず
                    var newPath = RailGraphDatastore.FindShortestPath(approaching, _destinationNode);
                    if (newPath.Count < 2)
                    {
                        if (_isAutoRun)
                        {
                            _isAutoRun = false;
                            _currentSpeed = 0;
                            throw new InvalidOperationException("自動運転で目的地までの経路が見つからない");
                        }
                        else
                        {
                            isRandomPathUse = true; //経路が見つからない手動の場合はランダム経路選択をする
                        }
                    }
                    else//見つかったので一番いいルートを自動選択
                    {
                        isRandomPathUse = false;
                        _railPosition.AddNodeToHead(newPath[1]);//newPath[0]はapproachingがはいってる
                                                                //残りの距離を再更新
                        _remainingDistance = RailNodeCalculate.CalculateTotalDistanceF(newPath);//計算量NlogN(logはnodeからintの辞書アクセス)
                    }
                }

                if (isRandomPathUse)
                {
                    //approachingから次のノードをランダムに取得して_railPosition.AddNodeToHead
                    var nextNodelist = approaching.ConnectedNodes.ToList();
                    if (nextNodelist.Count == 0) 
                    {
                        _currentSpeed = 0;
                        break;//もう進めない
                    }
                    var nextNode = nextNodelist[UnityEngine.Random.Range(0, nextNodelist.Count)];
                    _railPosition.AddNodeToHead(nextNode);
                }
                //----------------------------------------------------------------------------------------

                loopCount++;
                if (loopCount > 1000000)
                {
                    throw new InvalidOperationException("列車速度が無限に近いか、レール経路の無限ループを検知しました。");
                    break;
                }
            }
            return distanceToMove;
        }


        //毎フレーム燃料の在庫を確認しながら加速力を計算する
        public double UpdateTractionForce(double deltaTime)
        {
            var totalWeight = 0;
            var totalTraction = 0;

            foreach (var car in _cars)
            {
                var (weight, traction) = car.GetWeightAndTraction();//deltaTimeに応じて燃料が消費される未実装
                totalWeight += weight;
                totalTraction += traction;
            }
            return (double)(totalTraction) / totalWeight;
        }


        private bool IsArrivedDestination()
        {
            var node = _railPosition.GetNodeApproaching();
            if ((node == _destinationNode) & (_railPosition.GetDistanceToNextNode() == 0))
            {
                return true;
            }
            return false;
        }


        public void SetDestination(RailNode destination)
        {
            _destinationNode = destination;
        }

        public void TurnOnAutoRun()
        {
            var destinationNode = trainDiagram.GetNextDestination();
            if (destinationNode == null)
            {
                trainDiagram.MoveToNextEntry();
                destinationNode = trainDiagram.GetNextDestination();
            }

            if (destinationNode == null)
            {
                _isAutoRun = false;
                return;
            }

            _destinationNode = destinationNode;

            var approaching = _railPosition.GetNodeApproaching();
            if (approaching == null)
            {
                _isAutoRun = false;
                return;
            }

            if (approaching == _destinationNode)
            {
                var distanceToNextNode = _railPosition.GetDistanceToNextNode();
                if (distanceToNextNode == 0)
                {
                    _remainingDistance = distanceToNextNode;
                    _isAutoRun = true;
                    return;
                }

                _remainingDistance = distanceToNextNode;
                _isAutoRun = true;
                return;
            }

            var newPath = RailGraphDatastore.FindShortestPath(approaching, _destinationNode);
            if (newPath == null || newPath.Count < 2)
            {
                _remainingDistance = int.MaxValue;
                _isAutoRun = false;
                return;
            }

            _remainingDistance = RailNodeCalculate.CalculateTotalDistanceF(newPath) - _railPosition.GetDistanceToNextNode();
            _isAutoRun = true;
        }

        public void TurnOffAutoRun()
        {
            _isAutoRun = false;
        }



        //列車編成を保存する。ブロックとは違うことに注意
        public string GetSaveState()
        {
            return "";
        }

        //============================================================
        // ▼ ここからが「編成を分割する」ための処理例
        //============================================================
        /// <summary>
        ///  列車を「後ろから numberOfCars 両」切り離して、後ろの部分を新しいTrainUnitとして返す
        ///  新しいTrainUnitのrailpositionは、切り離した車両の長さに応じて調整される
        ///  新しいTrainUnitのtrainDiagramは空になる
        ///  新しいTrainUnitのドッキング状態はcarに情報があるためそのまま保存される
        /// </summary>
        public TrainUnit SplitTrain(int numberOfCarsToDetach)
        {
            // 例：10両 → 5両 + 5両など
            // 後ろから 5両を抜き取るケースを想定
            if (numberOfCarsToDetach <= 0 || numberOfCarsToDetach >= _cars.Count)
            {
                UnityEngine.Debug.LogError("SplitTrain: 指定両数が不正です。");
                return null;
            }
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
                detachedCars,
                _destinationNode  // 同じ目的地
                
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

        private void OnDestroy()
        {
            // 列車が破棄されるときに、ダイアグラムを解除
            TrainDiagramManager.Instance.UnregisterDiagram(this);
            TrainUpdateService.Instance.UnregisterTrain(this);
            trainUnitStationDocking = null;
            trainDiagram = null;
        }
    }

}
