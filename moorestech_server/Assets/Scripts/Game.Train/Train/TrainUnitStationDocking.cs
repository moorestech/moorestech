using System;
using System.Collections.Generic;
using System.Linq;
using Game.Context;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Train.Common;
using Game.Train.RailGraph;

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
        //なのでセーブ・ロードではcar側のboolのみを対象にしてstation側はセーブしない(?)

        //他メモ
        //ドッキング中の列車は削除できる→TODO真ん中削除したらどうなるか？
        //列車が乗っているnodeは削除できない
        //

        private TrainUnit _trainUnit;
        private RailNode _dockedNode = null; //ドッキングしているnode
        public RailNode DockedNode => _dockedNode;

        private sealed class DockedReceiver
        {
            public DockedReceiver(ITrainDockingReceiver receiver, TrainDockHandle handle)
            {
                Receiver = receiver;
                Handle = handle;
            }

            public ITrainDockingReceiver Receiver { get; }
            public TrainDockHandle Handle { get; }
        }

        private readonly Dictionary<IBlock, DockedReceiver> _dockedReceivers = new();

        //これは列車全体TrainCarを調査し一つでもドッキングしていたらドッキングしているとみなす
        public bool IsDocked => _trainUnit._cars.Any(car => car.IsDocked);

        public TrainUnitStationDocking(TrainUnit trainUnit)
        {
            _trainUnit = trainUnit;
        }


        public void TickDockedStations()
        {
            if (_dockedReceivers.Count == 0)
            {
                return;
            }

            foreach (var entry in _dockedReceivers.Values.ToArray())
            {
                entry.Receiver.OnTrainDockedTick(entry.Handle);
            }
        }


        //すべてのTrainCarのドッキング状態をfalseにする
        public void UndockFromStation()
        {
            _dockedNode = null;
            if (_dockedReceivers.Count > 0)
            {
                foreach (var entry in _dockedReceivers.Values.ToArray())
                {
                    entry.Receiver.OnTrainUndocked(entry.Handle);
                }
                _dockedReceivers.Clear();
            }

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
            for (int carIndex = 0; carIndex < _trainUnit._cars.Count; carIndex++)
            {
                var car = _trainUnit._cars[carIndex];
                // 車両の前端位置 = carposition
                var frontNodelist = _trainUnit._railPosition.GetNodesAtDistance(carposition);
                // 車両の後端位置 = carposition + car.Length
                carposition += car.Length;
                var rearNodelist = _trainUnit._railPosition.GetNodesAtDistance(carposition);

                car.dockingblock = null;

                // frontとrearのノードのStationRefを参照して、同じ駅にいるかつ前輪が駅の前、後輪が駅の後ろにある、という組み合わせが一つでもあれば合格
                if (frontNodelist != null && rearNodelist != null)
                {
                    bool flag = false; // breakフラグ
                    foreach (var frontNode in frontNodelist)
                    {
                        foreach (var rearNode in rearNodelist)
                        {
                            if (!IsSameStation(frontNode, rearNode))
                            {
                                continue;
                            }

                            var dockingBlock = frontNode.StationRef.StationBlock;
                            if (!RegisterDockingBlock(dockingBlock, car, carIndex))
                            {
                                continue;
                            }

                            car.dockingblock = dockingBlock;
                            flag = true;
                            _dockedNode = _trainUnit._railPosition.GetNodeApproaching();//駅にドッキングするということはdiagramで見ているエントリーのnodeの駅にドッキングするということ
                            break;
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
            return frontNode.StationRef.NodeRole == StationNodeRole.Exit;
        }


        private bool RegisterDockingBlock(IBlock block, TrainCar car, int carIndex)
        {
            if (block == null)
            {
                return false;
            }

            if (_dockedReceivers.TryGetValue(block, out var existing))
            {
                return existing.Handle.CarId == car.CarId;
            }

            if (block.ComponentManager.TryGetComponent<ITrainDockingReceiver>(out var receiver))
            {
                var handle = new TrainDockHandle(_trainUnit, car, carIndex);
                if (!receiver.CanDock(handle))
                {
                    return false;
                }

                _dockedReceivers[block] = new DockedReceiver(receiver, handle);
                receiver.OnTrainDocked(handle);
                return true;
            }

            return false;
        }

    }
}


