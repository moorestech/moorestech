using System;
using System.Collections.Generic;
using System.Linq;
using Game.Train.RailGraph;
using UnityEngine;

namespace Game.Train.Train
{
    public class TrainUnit
    {
        //そのうちprivateにする
        public RailPosition _railPosition;
        public RailNode _destination; // 目的地（駅ノードなど）
        public bool _isUseDestination;
        public float _currentSpeed;   // m/s など適宜
        //摩擦係数、空気抵抗係数などはここに追加する
        const float FRICTION = 0.0002f;
        const float AIR_RESISTANCE = 0.00002f;

        private List<TrainCar> _cars;

        public TrainUnit(
            RailPosition initialPosition,
            RailNode destination,
            List<TrainCar> cars
        )
        {
            _railPosition = initialPosition;
            _destination = destination;
            _cars = cars;  // 追加
            _currentSpeed = 0.0f; // 仮の初期速度
            _isUseDestination = false;
        }


        public void UpdateTrain(float deltaTime, out int calceddist) 
        { 
            if (_isUseDestination)//設定している目的地に向かうべきなら
            {
                var force = UpdateTractionForce(deltaTime);
                _currentSpeed += force * deltaTime;
            }
            //どちらにしても速度は摩擦で減少する。摩擦は速度の1乗、空気抵抗は速度の2乗に比例するとする
            //deltaTime次第でかわる
            _currentSpeed -= _currentSpeed* deltaTime * FRICTION + _currentSpeed* _currentSpeed * deltaTime * AIR_RESISTANCE;
            //速度が0以下にならないようにする
            _currentSpeed = Mathf.Max(0, _currentSpeed);

            float floatDistance = _currentSpeed * deltaTime;
            //floatDistanceが1.5ならランダムで1か2になる
            //floatDistanceが-1.5ならランダムで-1か-2になる
            int distanceToMove = Mathf.FloorToInt(floatDistance + UnityEngine.Random.Range(0f, 0.999f));
            calceddist = UpdateTrainByDistance(distanceToMove);
        }

        //ゲームは基本こっちの関数を使う
        //distanceToMoveの距離絶対進むが、唯一目的地についたときだけ残りの距離を返す
        public int UpdateTrainByDistance(int distanceToMove) 
        {
            //進行メインループ
            //何かが原因で無限ループになることがあるので、一定回数で強制終了する
            int loopCount = 0;
            while (distanceToMove != 0)
            {
                distanceToMove = _railPosition.MoveForward(distanceToMove);
                //目的地に到着してたら速度を0にする
                if (IsArrivedDestination())
                {
                    _currentSpeed = 0;
                    _isUseDestination = false;
                    break;
                }
                if (distanceToMove == 0) break;
                //distanceToMoveが0以外かつある分岐地点についてる状況
                var isContinue = CheckAndHandleBranch();//次の経路をセット
                if (!isContinue)
                {
                    _isUseDestination = false;
                    _currentSpeed = 0;
                    throw new InvalidOperationException("列車が進行中に目的地までの経路を見失いました。");
                    break;
                }
                loopCount++;
                if (loopCount > 10000)
                {
                    throw new InvalidOperationException("列車速度が無限に近いか、レール経路の無限ループを検知しました。");
                    break;
                }
            }
            return distanceToMove;
        }


        //毎フレーム燃料の在庫を確認しながら加速力を計算する
        public float UpdateTractionForce(float deltaTime)
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

        //返り値はtrueならまだ進行するべきである
        private bool CheckAndHandleBranch()
        {
            RailNode approaching = _railPosition.GetNodeApproaching();
            if (approaching == null)
            {
                return false;
            }
            //目的地についていたら。これはチェック済み
            /*if (approaching == _destination)
            {
                _currentSpeed = 0;
                _isUseDestination = false;
                return false;
            }*/

            var connectedNodes = approaching.ConnectedNodes.ToList();
            //分岐先のむこうが1つなのでそのまま進む。経路探索はしない(デメリットは手動で経路をかえたあと、列車が目的地までの経路が存在しないときにとまれないこと)
            if (connectedNodes.Count < 2)
            {
                _railPosition.AddNodeToHead(connectedNodes[0]);
                return true;
            }
            //分岐先が2つ以上ある場合は、最短経路を再度探す。最低でも返りlistにはapproaching, _destinationが入っているはず
            var newPath = RailGraphDatastore.FindShortestPath(approaching, _destination);
            if (newPath.Count < 2)
            {
                return false;
            }
            _railPosition.AddNodeToHead(newPath[1]);
            return true;
        }

        private bool IsArrivedDestination()
        {
            var node = _railPosition.GetNodeApproaching();
            if ((node == _destination) & (_railPosition.GetDistanceToNextNode() == 0))
            {
                return true;
            }
            return false;
        }

        public void SetSpeed(float newSpeed)
        {
            _currentSpeed = newSpeed;
        }

        public void SetDestination(RailNode destination)
        {
            _destination = destination;
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
            // (前から分割したいなら別途実装)

            if (numberOfCarsToDetach <= 0 || numberOfCarsToDetach >= _cars.Count)
            {
                Debug.LogError("SplitTrain: 指定両数が不正です。");
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
                _destination,  // 同じ目的地
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
    }

}

