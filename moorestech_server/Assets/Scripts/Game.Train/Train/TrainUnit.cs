using System.Collections.Generic;
using System.Linq;
using Game.Train.RailGraph;
using UnityEngine;

namespace Game.Train.Train
{
    public class TrainUnit
    {
        private RailPosition _railPosition;

        private RailNode _destination; // 目的地（駅ノードなど）
        private float _currentSpeed;   // m/s など適宜

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
            _currentSpeed = 5.0f; // 仮の初期速度
        }

        public void UpdateTrain(float deltaTime)
        {
            float floatDistance = _currentSpeed * deltaTime;
            //floatDistanceが1.5ならランダムで1か2になる
            //floatDistanceが-1.5ならランダムで-1か-2になる
            int distanceToMove = Mathf.FloorToInt(floatDistance + Random.Range(0f, 0.999f));
            
            int leftover = _railPosition.MoveForward(distanceToMove);
            CheckAndHandleBranch(leftover);

            while (leftover != 0)
            {
                leftover = _railPosition.MoveForward(leftover);
                CheckAndHandleBranch(leftover);
            }

            if (IsArrivedDestination())
            {
                _currentSpeed = 0;
            }
        }

        private void CheckAndHandleBranch(int leftover)
        {
            RailNode approaching = _railPosition.GetNodeApproaching();
            if (approaching == null) return;

            var connectedNodes = approaching.ConnectedNodes.ToList();
            if (connectedNodes.Count < 2) return;

            var newPath = RailGraphDatastore.FindShortestPath(approaching, _destination);
            if (newPath.Count < 2) return;

            _railPosition.AddNodeToHead(newPath[1]);
        }

        private bool IsArrivedDestination()
        {
            var nodes = _railPosition.GetNodeApproaching();
            if (nodes == _destination)
            {
                return true;
            }
            return false;
        }

        public void SetSpeed(float newSpeed)
        {
            _currentSpeed = newSpeed;
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

            // 2) 既存のTrainUnitからは そのぶん削除
            _cars.RemoveRange(_cars.Count - numberOfCarsToDetach, numberOfCarsToDetach);
            // _carsの両数に応じて、列車長を算出する
            int newTrainLength = 0;
            foreach (var car in _cars)
                newTrainLength += car.Length;
            _railPosition.SetTrainLength(newTrainLength);

            // 3) 新しく後ろのTrainUnitを作る
            var splittedRailPosition = CreateSplittedRailPosition(detachedCars);

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

