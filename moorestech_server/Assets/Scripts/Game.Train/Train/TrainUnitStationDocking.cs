using System;
using System.Collections.Generic;
using System.Linq;
using Game.Context;
using Game.Train.RailGraph;
//using Game.Block.Blocks.TrainRail;

namespace Game.Train.Train
{
    // TrainUnit全体がドッキングしているかどうか
    public class TrainUnitStationDocking
    {
        //TrainUnitの_carsと_railPositionを参照したいのでTrainUnitを受け取って列車の速度が0になったとき(手動自動とも)に列車の端がぴったり乗るnodeを検出
        //それが同じ駅の前と後ろにぴったり重なっている場合ドッキング成立とする
        //この検出はここで逐次計算で行う

        //状態について
        //・列車が駅にドッキングしてない状態
        //・列車が駅にドッキングしている状態
        //  ・この場合各carごとにドッキングしているかしてないかboolをもつ
        //  ・station側でないのは、セーブ・ロードなどなにかの操作で列車がロストした場合永久にstationがロックされるのを防ぐため
        //
        //station側ではcarがドッキングした瞬間にstationのドッキング状態を更新する
        //これでロード時最初の1フレームで必ずドッキングされた状態で始まる
        //なのでセーブ・ロードではcar側のboolのみを対象にしてstation側はセーブしない

        //他メモ
        //ドッキング中の列車は削除できる→TODO真ん中削除したらどうなるか？
        //列車が乗っているnodeは削除できない
        //

        // 各車両のドッキング状態を管理  
        private TrainUnit _trainUnit;

        public TrainUnitStationDocking(TrainUnit trainUnit)
        {
            _trainUnit = trainUnit;
        }

        /// <summary>  
        /// 列車の速度が0の時にドッキング状態をチェック  
        /// </summary>  
        public void CheckDockingStatus()
        {
            /*
            // 速度が0でない場合はドッキングチェックしない  
            if (_trainUnit._currentSpeed > 0)
            {
                if (_isDocked)
                {
                    UndockFromStation();
                }
                return;
            }

            // 列車の前端と後端のノードを取得してドッキング判定  
            var frontNode = _trainUnit._railPosition.GetNodeApproaching();
            var rearNode = GetTrainRearNode();

            if (frontNode != null && rearNode != null && IsSameStation(frontNode, rearNode))
            {
                DockToStation();
            }
            else if (_isDocked)
            {
                UndockFromStation();
            }
            */
        }

        private RailNode GetTrainRearNode()
        {
            // 列車長を考慮して後端ノードを計算  
            // 実装は列車の長さと現在位置から後端を特定する必要がある  
            return null; // 簡略化  
        }

        private bool IsSameStation(RailNode frontNode, RailNode rearNode)
        {
            // 同じ駅の前後ノードかチェック  
            // 実装は駅ブロックとの関連付けが必要  
            return false; // 簡略化  
        }

        private void DockToStation()
        {
            /*
            if (!_isDocked)
            {
                _isDocked = true;

                // 各車両のドッキング状態を設定  
                foreach (var car in _trainUnit._cars)
                {
                    _carDockingStates[car] = true;
                }
            }
            */
        }

        private void UndockFromStation()
        {
            /*
            if (_isDocked)
            {
                _isDocked = false;
                _carDockingStates.Clear();
            }
            */
        }
        /*
        public bool IsCarDocked(TrainCar car)
        {
            return _carDockingStates.ContainsKey(car) && _carDockingStates[car];
        }

        public bool IsTrainDocked()
        {
            return _isDocked;
        }
        */
    }
}


