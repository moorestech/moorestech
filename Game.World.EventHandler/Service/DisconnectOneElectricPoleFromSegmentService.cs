using System;
using System.Collections.Generic;
using Core.Block.Blocks;
using Core.Block.Config;
using Core.Block.Config.LoadConfig.Param;
using Core.Electric;
using Game.World.Interface.DataStore;

namespace Game.World.EventHandler.Service
{
    public class DisconnectOneElectricPoleFromSegmentService
    {
        private readonly IWorldBlockComponentDatastore<IBlockElectric> _electricDatastore;
        private readonly IWorldBlockComponentDatastore<IPowerGenerator> _powerGeneratorDatastore;
        private readonly IWorldBlockComponentDatastore<IElectricPole> _electricPoleDatastore;
        private readonly IWorldElectricSegmentDatastore _worldElectricSegmentDatastore;
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IBlockConfig _blockConfig;

        public DisconnectOneElectricPoleFromSegmentService(
            IBlockConfig blockConfig, 
            IWorldBlockDatastore worldBlockDatastore, 
            IWorldBlockComponentDatastore<IPowerGenerator> powerGeneratorDatastore, 
            IWorldBlockComponentDatastore<IBlockElectric> electricDatastore, 
            IWorldElectricSegmentDatastore worldElectricSegmentDatastore, 
            IWorldBlockComponentDatastore<IElectricPole> electricPoleDatastore)
        {
            _blockConfig = blockConfig;
            _worldBlockDatastore = worldBlockDatastore;
            _powerGeneratorDatastore = powerGeneratorDatastore;
            _electricDatastore = electricDatastore;
            _worldElectricSegmentDatastore = worldElectricSegmentDatastore;
            _electricPoleDatastore = electricPoleDatastore;
        }

        public void Disconnect(IElectricPole removedElectricPole)
        {
            //必要なデータを取得
            var (x, y) = _worldBlockDatastore.GetBlockPosition(removedElectricPole.EntityId);
            var poleConfig =
                _blockConfig.GetBlockConfig(((IBlock)removedElectricPole).BlockId).Param as ElectricPoleConfigParam;
            var removedSegment = _worldElectricSegmentDatastore.GetElectricSegment(removedElectricPole);
            var electricPoles = new FindElectricPoleFromPeripheralService().Find(
                x, y, poleConfig, _electricPoleDatastore);
            
            if (electricPoles.Count != 1)
            {
                throw new Exception("周辺の電柱が1つではありません");
            }
            
            
            //セグメントから電柱の接続状態を解除
            removedSegment.RemoveElectricPole(removedElectricPole);

            //周辺の機械、発電機を取得
            var (blocks, generators) =
                new FindMachineAndGeneratorFromPeripheralService().Find(x, y, poleConfig, _electricDatastore,
                    _powerGeneratorDatastore);

            //周辺の機械、発電機を接続状態から解除する
            blocks.ForEach(removedSegment.RemoveBlockElectric);
            generators.ForEach(removedSegment.RemoveGenerator);


            //繋がっていた1つの電柱の周辺の機械と発電機を探索
            var (connectedX, connectedY) = _worldBlockDatastore.GetBlockPosition(electricPoles[0].EntityId);
            var connectedPoleConfig =
                _blockConfig.GetBlockConfig(((IBlock) electricPoles[0]).BlockId).Param as ElectricPoleConfigParam;
            var (connectedBlocks, connectedGenerators) =
                new FindMachineAndGeneratorFromPeripheralService().Find(connectedX, connectedY,
                    connectedPoleConfig, _electricDatastore, _powerGeneratorDatastore);

            //セグメントに追加する
            connectedBlocks.ForEach(removedSegment.AddBlockElectric);
            connectedGenerators.ForEach(removedSegment.AddGenerator);
        }
    }
}