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
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IBlockConfig _blockConfig;

        public DisconnectOneElectricPoleFromSegmentService(
            IBlockConfig blockConfig, 
            IWorldBlockDatastore worldBlockDatastore, 
            IWorldBlockComponentDatastore<IPowerGenerator> powerGeneratorDatastore, 
            IWorldBlockComponentDatastore<IBlockElectric> electricDatastore)
        {
            _blockConfig = blockConfig;
            _worldBlockDatastore = worldBlockDatastore;
            _powerGeneratorDatastore = powerGeneratorDatastore;
            _electricDatastore = electricDatastore;
        }

        public void Disconnect(
            ElectricSegment removedSegment,
            IElectricPole removedElectricPole,
            int x, int y,
            ElectricPoleConfigParam poleConfig,
            IReadOnlyList<IElectricPole> electricPoles)
        {
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
            var (connectedX, connectedY) = _worldBlockDatastore.GetBlockPosition(electricPoles[0].GetIntId());
            var connectedPoleConfig =
                _blockConfig.GetBlockConfig(((IBlock) electricPoles[0]).GetBlockId()).Param as ElectricPoleConfigParam;
            var (connectedBlocks, connectedGenerators) =
                new FindMachineAndGeneratorFromPeripheralService().Find(connectedX, connectedY,
                    connectedPoleConfig, _electricDatastore, _powerGeneratorDatastore);

            //セグメントに追加する
            connectedBlocks.ForEach(removedSegment.AddBlockElectric);
            connectedGenerators.ForEach(removedSegment.AddGenerator);
        }
    }
}