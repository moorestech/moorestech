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

        //これは列車全体TrainCarを調査し一つでもドッキングしていたらドッキングしているとみなす
        public bool IsDocked => _trainUnit._cars.Any(car => car.IsDocked);

        public TrainUnitStationDocking(TrainUnit trainUnit)
        {
            _trainUnit = trainUnit;
        }



        //すべてのTrainCarのドッキング状態をfalseにする
        public void UndockFromStation()
        {
            // 各車両のドッキング状態をリセット  
            foreach (var car in _trainUnit._cars)
            {
                car.dockingblock = null; // ドッキング状態を解除  
            }
        }


        /// <summary>    
        /// trainunitのrailpositionを参照して、carの前端と後端のノードを取得し、同じ駅にドッキングできるかチェックする  
        /// ドッキングできるなら各carのドッキング状態を更新する
        /// </summary>
        public void TryDockWhenStopped()
        {
            
            if (_trainUnit._cars.Count == 0 || _trainUnit._railPosition == null)
            {
                return; // 列車が存在しない場合は何もしない
            }

            //GetNodesAtDistanceをつかう
            //列車を先頭から順にみていく
            int carposition = 0;
            foreach (var car in _trainUnit._cars)
            {
                // 車両の前端位置 = carposition
                var frontNodelist = _trainUnit._railPosition.GetNodesAtDistance(carposition);
                // 車両の後端位置 = carposition + car.Length
                carposition += car.Length;
                var rearNodelist = _trainUnit._railPosition.GetNodesAtDistance(carposition);

                // frontとrearのノードのStationRefを参照して、同じ駅にいるかつ前輪が駅の前、後輪が駅の後ろにある、という組み合わせが一つでもあれば合格
                if (frontNodelist != null && rearNodelist != null)
                {
                    bool flag = false; // breakフラグ
                    foreach (var frontNode in frontNodelist)
                    {
                        foreach (var rearNode in rearNodelist)
                        {
                            // 同じ駅に属するかチェック  
                            if (IsSameStation(frontNode, rearNode))
                            {
                                // ドッキング状態を更新  
                                car.dockingblock = frontNode.StationRef.StationBlock; // 前端ノードをドッキングブロックとする  
                                flag = true;
                                break;
                            }
                        }
                        if (flag) break;
                    }
                }
            }

        }

        /// <summary>  
        /// 2つのノードが同じ駅に属するかチェック  
        /// フロントがちゃんと前かもチェック
        /// </summary>  
        private bool IsSameStation(RailNode frontNode, RailNode rearNode)
        {
            bool isPair = frontNode.StationRef.IsPairWith(rearNode.StationRef); // StationReferenceのペアチェック
            if (!isPair)
            {
                return false; // 同じ駅でない場合はfalse  
            }
            return frontNode.StationRef.NodeRole == StationNodeRole.Entry;
        }


    }
}


