using System;
using System.Collections.Generic;
using System.Linq;
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
        private int _remainingDistance;
        private bool _isAutoRun;
        public bool IsAutoRun
        {
            get { return _isAutoRun; }
        }

        public double _currentSpeed;   // m/s など適宜
        //摩擦係数、空気抵抗係数などはここに追加する
        const double FRICTION = 0.0002f;
        const double AIR_RESISTANCE = 0.00002f;

        public List<TrainCar> _cars;
        TrainUnitStationDocking trainUnitStationDocking; // 列車の駅ドッキング用のクラス


        public TrainUnit(
            RailPosition initialPosition,
            List<TrainCar> cars,
            RailNode destination = null
        )
        {
            _railPosition = initialPosition;
            _destinationNode = destination;
            _cars = cars;  // 追加
            _currentSpeed = 0.0; // 仮の初期速度
            _isAutoRun = false;
            trainUnitStationDocking = new TrainUnitStationDocking(this);
        }


        // Updateの時間版
        // 進んだ距離を返す
        public int UpdateTrain(double deltaTime) 
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
                //自動運転で目的地に到着してたら速度を0にする
                if (IsArrivedDestination() && _isAutoRun)
                {
                    _currentSpeed = 0;
                    _isAutoRun = false;
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
        public float UpdateTractionForce(double deltaTime)
        {
            var totalWeight = 0;
            var totalTraction = 0;

            foreach (var car in _cars)
            {
                var (weight, traction) = car.GetWeightAndTraction();//deltaTimeに応じて燃料が消費される未実装
                totalWeight += weight;
                totalTraction += traction;
            }
            return (float)(totalTraction) / totalWeight;
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
            _isAutoRun = true;
            RailNode approaching = _railPosition.GetNodeApproaching();
            //経路に到達していたら
            if (approaching == _destinationNode)
            {
                if (_railPosition.GetDistanceToNextNode() == 0)
                {
                    _remainingDistance = 0;
                    _isAutoRun = false;
                    return;
                }
                else
                {
                    //経路に到達しているが、まだ次のノードに進んでいない場合は残り距離を更新
                    _remainingDistance = _railPosition.GetDistanceToNextNode();
                    return;
                }
            }

            var newPath = RailGraphDatastore.FindShortestPath(approaching, _destinationNode);
            //経路が見つかった場合，最低でも返りlistにはapproaching, _destinationNodeが入っているはず
            //経路が見つからない場合とりあえずmaxをいれとく
            //経路に到達していたらは↑
            if (newPath.Count < 2)
            {
                //経路が見つからない場合とりあえずmaxをいれとく
                _remainingDistance = int.MaxValue;
                return;
            }
            //残りの距離を更新
            _remainingDistance = RailNodeCalculate.CalculateTotalDistanceF(newPath) - _railPosition.GetDistanceToNextNode();
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
        ///  列車を「後ろから numberOfCars 両」切り離して新しいTrainUnitとして返す
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
    }

}