using System.Collections.Generic;
using System.Linq;
using Game.Train.RailGraph;
using UnityEngine;

namespace Game.Train.Train
{
    public class TrainUnit
    {
        private RailPosition _railPosition;
        private RailGraphDatastore _railGraph;

        private RailNode _destination; // 目的地（駅ノードなど）
        private float _currentSpeed;   // m/s など適宜

        private List<TrainCar> _cars;

        public TrainUnit(
            RailPosition initialPosition,
            RailNode destination,
            RailGraphDatastore graph,
            List<TrainCar> cars
        )
        {
            _railPosition = initialPosition;
            _destination = destination;
            _railGraph = graph;
            _cars = cars;  // 追加

            _currentSpeed = 5.0f; // 仮の初期速度
        }

        public void UpdateTrain(float deltaTime)
        {
            float floatDistance = _currentSpeed * deltaTime;
            int distanceToMove = Mathf.FloorToInt(floatDistance);
            if (distanceToMove <= 0) return;

            int leftover = _railPosition.MoveForward(distanceToMove);
            CheckAndHandleBranch(leftover);

            while (leftover > 0)
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

            // ※ _railPosition の現在の車両長などから初期距離を調整すべき
            int newInitialDistance = _railPosition.GetDistanceToNextNode();
            int trainLength = _railPosition.TestGet_railNodes().Count; // (単純例)

            var newRailPos = new RailPosition(newPath, trainLength, newInitialDistance);
            _railPosition = newRailPos;
        }

        private bool IsArrivedDestination()
        {
            var nodes = _railPosition.TestGet_railNodes();
            if (nodes.Count == 1 && nodes[0] == _destination)
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

            // 3) 新しくTrainUnitを作る
            //    分割後の編成は「同じ位置(RailPosition) だけど、車両長が異なる」など
            //    ここで "後方列車" 用の RailPosition をどうするかが問題
            //    → 多くの場合「後ろにいる分、initialDistanceToNextNode を足す」などの調整が必要

            // ※以下では単純に "同じ RailPosition を複製" し、列車長を差し引く といった例
            //    ただし実際には "後続列車はもう少し後方にいる" などの再計算が必要

            var splittedRailPosition = CreateSplittedRailPosition(detachedCars);

            // 4) 新しいTrainUnitを作成
            var splittedUnit = new TrainUnit(
                splittedRailPosition,
                _destination,  // 同じ目的地
                _railGraph,
                detachedCars
            );

            // 5) 既存のTrainUnit も自分の車両数が減ったので、
            //    それに合わせて _railPosition の車両長などを調整
            AdjustOwnRailPositionAfterSplit();

            // 6) 新しいTrainUnitを返す
            return splittedUnit;
        }

        /// <summary>
        /// 後続列車のために、新しいRailPositionを生成し返す。
        /// ここでは単純に「現在と同じRailNodeリストを持つが、trainLengthだけ差し替える」例。
        /// </summary>
        private RailPosition CreateSplittedRailPosition(List<TrainCar> splittedCars)
        {
            // 既存のルートを複製 (実際には深いコピーなど必要な場合もある)
            var oldNodes = _railPosition.TestGet_railNodes();
            var newNodeList = oldNodes.ToList();

            // splittedCarsの両数に応じて、列車長を算出する例
            int splittedTrainLength = 0;
            foreach (var car in splittedCars)
                splittedTrainLength += car.length;

            // 後続列車は先頭が少し後ろにいる可能性が高いが、ここでは簡略化して 0 とする
            int splittedInitialDistance = 0;

            var splittedRailPos = new RailPosition(newNodeList, splittedTrainLength, splittedInitialDistance);
            return splittedRailPos;
        }

        /// <summary>
        /// 自身の車両が減ったあとのレール上の調整
        /// </summary>
        private void AdjustOwnRailPositionAfterSplit()
        {
            // 今の _cars にあわせて _railPosition の列車長を再設定する
            // 例えば 1両=10m とする
            int newTrainLength = 0;
            foreach (var car in _cars)
                newTrainLength += car.length;

            // RailPosition には private に _trainLength があるが、直接は変更できないため
            //   → 新しい RailPosition を生成し直してもよい
            //   → あるいはリフレクションで無理やり書き換える(非推奨)
            // ここでは例として「同じノード列 + newTrainLength」を再構築する
            var oldNodes = _railPosition.TestGet_railNodes();
            int oldDistance = _railPosition.GetDistanceToNextNode();
            // 新しいRailPosition
            var newPos = new RailPosition(oldNodes, newTrainLength, oldDistance);
            _railPosition = newPos;
        }
    }

}

