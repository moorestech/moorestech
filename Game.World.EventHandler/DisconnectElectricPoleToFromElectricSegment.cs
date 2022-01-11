using System.Collections.Generic;
using Core.Block.Blocks;
using Core.Block.Config;
using Core.Block.Config.LoadConfig.Param;
using Core.Electric;
using Game.World.EventHandler.Service;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;

namespace Game.World.EventHandler
{
    public class DisconnectElectricPoleToFromElectricSegment
    {
        private readonly IWorldBlockComponentDatastore<IElectricPole> _electricPoleDatastore;
        private readonly IBlockConfig _blockConfig;
        private readonly IWorldElectricSegmentDatastore _worldElectricSegmentDatastore;
        private readonly DisconnectOneElectricPoleFromSegmentService _disconnectOne;

        private readonly DisconnectTwoOreMoreElectricPoleFromSegmentService
            _disconnectTwoOreMore;

        public DisconnectElectricPoleToFromElectricSegment(
            IBlockRemoveEvent blockRemoveEvent,
            IWorldBlockComponentDatastore<IElectricPole> electricPoleDatastore,
            IBlockConfig blockConfig,
            IWorldElectricSegmentDatastore worldElectricSegmentDatastore, 
            DisconnectOneElectricPoleFromSegmentService disconnectOne, 
            DisconnectTwoOreMoreElectricPoleFromSegmentService disconnectTwoOreMore)
        {
            _electricPoleDatastore = electricPoleDatastore;
            _blockConfig = blockConfig;
            _worldElectricSegmentDatastore = worldElectricSegmentDatastore;
            _disconnectOne = disconnectOne;
            _disconnectTwoOreMore = disconnectTwoOreMore;
            blockRemoveEvent.Subscribe(OnBlockRemove);
        }

        private void OnBlockRemove(BlockRemoveEventProperties blockRemoveEvent)
        {
            var x = blockRemoveEvent.Coordinate.X;
            var y = blockRemoveEvent.Coordinate.Y;

            //電柱かどうか判定
            //電柱だったら接続範囲内周りにある電柱を取得する
            if (!_electricPoleDatastore.ExistsComponentBlock(x, y)) return;
            


            //接続範囲内の電柱を取得
            var electricPoles = new FindElectricPoleFromPeripheralService().Find(
                    x, y, _blockConfig.GetBlockConfig(blockRemoveEvent.Block.GetBlockId()).Param as ElectricPoleConfigParam, _electricPoleDatastore);
            var removedElectricPole = _electricPoleDatastore.GetBlock(x, y);

            //削除した電柱のセグメントを取得
            var removedSegment = _worldElectricSegmentDatastore.GetElectricSegment(removedElectricPole);

            
            switch (electricPoles.Count)
            {
                //周りに電柱がないとき
                case 0:
                    //セグメントを削除する
                    _worldElectricSegmentDatastore.RemoveElectricSegment(removedSegment);
                    return;
                //周りの電柱が1つの時
                case 1:
                    _disconnectOne.Disconnect(removedElectricPole);
                    return;
                //周りの電柱が2つ以上の時
                case >= 2:
                    _disconnectTwoOreMore.Disconnect(removedElectricPole);
                    break;
            }
        }
    }
}