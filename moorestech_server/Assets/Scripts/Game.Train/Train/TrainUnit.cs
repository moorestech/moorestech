using System.Collections.Generic;
using UnityEngine;

namespace Game.Train.Train
{
    /// <summary>
    /// 列車一編成を表すクラス
    /// 未完成
    /// A class that represents a single train
    /// 
    /// 必要な情報は以下の通り
    /// ・編成している機関車のリスト,編成している貨車のリスト,前から順番に格納
    /// ・現在の速度,これは編成全体の速度
    /// ・目的地に向かって走っているか否か
    /// ・目的地に向かって走っているなら目的地までのノードリスト(距離付き)
    /// ・列車編成のグラフ上の位置はまずここで保持、各車両(の前輪と後輪)がこのクラスを参照して位置を更新する
    /// ・列車編成のグラフ上の位置は、List<RailNode>と、列車先頭の前輪の位置が、今目指しているRailNodeからどのくらい離れているか(int)の情報で場所が一意にきまる
    /// 
    /// 必要なメゾット
    /// ・現在の先頭貨車の前方ノードと目的地のノードを使用して、レールグラフに対しダイクストラ法などで目的地までの最短経路を求めさせ、今後通るノードのリスト(距離付き)を得る → 目的地設定時とレールグラフ変更時に呼び出す予定
    /// ・現在の先頭貨車の前方ノードを使用して、すべての行ける駅(または目的地)のノードのリストを得る → 表示用
    /// ・現在走っているなら目的地までのノードリストを参照して列車をすすめる。UniRx;のupdate使う感じですか？
    /// ・進行方向を反転する
    /// 
    /// </summary>
    public class TrainUnit
    {
        // 列車の編成
        private List<ITrainCar> _trainFormation;
        private float _currentSpeed;
        private bool _isRunning;
        private List<(RailNode node, int distance)> _destinationPath;//見直し予定

        public TrainUnit()
        {
            _trainFormation = new List<ITrainCar>();
            _currentSpeed = 0;
            _isRunning = false;
            _destinationPath = new List<(RailNode, int)>();
        }

        // 車両を追加
        public void AddTrainCar(ITrainCar trainCar)
        {
            if (trainCar == null)
            {
                Debug.LogError("Train car cannot be null.");
                return;
            }

            _trainFormation.Add(trainCar);
        }

        // 列車全体の速度を設定
        public void SetSpeed(float speed)
        {
            _currentSpeed = speed;
        }

        // 目的地を設定し、最短経路を計算
        public void SetDestination(RailNode destination, RailGraph railGraph)
        {
            if (_trainFormation.Count == 0) return;

            // 先頭車両の位置を取得
            var firstCar = _trainFormation[0] as TrainCarBase;
            if (firstCar?.FrontWheelPosition.from == null) return;

            // ダイクストラ法などを使用して最短経路を計算
            _destinationPath = railGraph.CalculateShortestPath(firstCar.FrontWheelPosition.from, destination);
            
        }

        // 現在の行ける駅を取得（表示用）
        public List<RailNode> GetReachableStations(RailGraph railGraph)
        {
            if (_trainFormation.Count == 0) return new List<RailNode>();

            var firstCar = _trainFormation[0] as TrainCarBase;
            return railGraph.GetAllReachableNodes(firstCar?.FrontWheelPosition.from);
        }

        // 進行方向を反転
        public void ReverseDirection()
        {
            _trainFormation.Reverse();
            //リストも反転
            //pass
        }

        // Update 処理で列車を進める（GPT版）
        public void UpdatePosition()
        {
            /*
            if (!_isRunning || _destinationPath.Count == 0 || _currentSpeed <= 0) return;

            // 各車両の位置を更新
            foreach (var car in _trainFormation)
            {
                var trainCar = car as TrainCarBase;
                if (trainCar != null)
                {
                    // 簡易的な位置更新（詳細実装は状況次第）
                    trainCar.FrontWheelDistanceFromStart += (int)_currentSpeed;
                    trainCar.RearWheelDistanceFromStart += (int)_currentSpeed;
                }
            }
            */

        }
    }
}