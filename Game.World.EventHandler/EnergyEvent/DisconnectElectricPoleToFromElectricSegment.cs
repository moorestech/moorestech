using System.Collections.Generic;
using Core.Block.Blocks;
using Core.Block.Config;
using Core.Block.Config.LoadConfig.Param;
using Core.Electric;
using Core.EnergySystem;
using Game.World.EventHandler.Service;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;

namespace Game.World.EventHandler
{
    public class DisconnectElectricPoleToFromElectricSegment<TSegment,TTransformer>
        where TSegment : EnergySegment, new()
        where TTransformer : IEnergyTransformer
    {
        private readonly IBlockConfig _blockConfig;
        private readonly IWorldEnergySegmentDatastore<TSegment> _worldEnergySegmentDatastore;
        private readonly DisconnectOneElectricPoleFromSegmentService<TSegment> _disconnectOne;
        private readonly IWorldBlockDatastore _worldBlockDatastore;

        private readonly DisconnectTwoOrMoreElectricPoleFromSegmentService<TSegment> _disconnectTwoOrMore;

        public DisconnectElectricPoleToFromElectricSegment(
            IBlockRemoveEvent blockRemoveEvent,
            IBlockConfig blockConfig,
            IWorldEnergySegmentDatastore<TSegment> worldEnergySegmentDatastore, 
            DisconnectOneElectricPoleFromSegmentService<TSegment> disconnectOne, 
            DisconnectTwoOrMoreElectricPoleFromSegmentService<TSegment> disconnectTwoOrMore,
            IWorldBlockDatastore worldBlockDatastore)
        {
            _blockConfig = blockConfig;
            _worldEnergySegmentDatastore = worldEnergySegmentDatastore;
            _disconnectOne = disconnectOne;
            _disconnectTwoOrMore = disconnectTwoOrMore;
            _worldBlockDatastore = worldBlockDatastore;
            blockRemoveEvent.Subscribe(OnBlockRemove);
        }

        private void OnBlockRemove(BlockRemoveEventProperties blockRemoveEvent)
        {
            var x = blockRemoveEvent.Coordinate.X;
            var y = blockRemoveEvent.Coordinate.Y;

            //電柱かどうか判定
            //電柱だったら接続範囲内周りにある電柱を取得する
            if (!_worldBlockDatastore.TryGetBlock<TTransformer>(x, y,out var removedElectricPole)) return;
            


            //接続範囲内の電柱を取得
            var electricPoles = FindElectricPoleFromPeripheralService.Find(
                    x, y, _blockConfig.GetBlockConfig(blockRemoveEvent.Block.BlockId).Param as ElectricPoleConfigParam, _worldBlockDatastore);

            //削除した電柱のセグメントを取得
            var removedSegment = _worldEnergySegmentDatastore.GetEnergySegment(removedElectricPole);

            
            switch (electricPoles.Count)
            {
                //周りに電柱がないとき
                case 0:
                    //セグメントを削除する
                    _worldEnergySegmentDatastore.RemoveEnergySegment(removedSegment);
                    return;
                //周りの電柱が1つの時
                case 1:
                    _disconnectOne.Disconnect(removedElectricPole);
                    return;
                //周りの電柱が2つ以上の時
                case >= 2:
                    _disconnectTwoOrMore.Disconnect(removedElectricPole);
                    break;
            }
        }
    }
}