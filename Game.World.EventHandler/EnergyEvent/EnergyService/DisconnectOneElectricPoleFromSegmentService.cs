using System;
using Core.Block.Blocks;
using Core.Block.Config.LoadConfig.Param;
using Core.Electric;
using Core.EnergySystem;
using Game.World.EventHandler.Service;

namespace Game.World.EventHandler.EnergyEvent.EnergyService
{
    public static class DisconnectOneElectricPoleFromSegmentService<TSegment,TConsumer,TGenerator,TTransformer> 
        where TSegment : EnergySegment, new()
        where TConsumer : IBlockElectricConsumer
        where TGenerator : IPowerGenerator
        where TTransformer : IEnergyTransformer
    {

        public static void Disconnect(IEnergyTransformer removedElectricPole,EnergyServiceDependencyContainer<TSegment> container)
        {
            //必要なデータを取得
            var (x, y) = container.WorldBlockDatastore.GetBlockPosition(removedElectricPole.EntityId);
            var poleConfig =
                container.BlockConfig.GetBlockConfig(((IBlock)removedElectricPole).BlockId).Param as ElectricPoleConfigParam;
            var removedSegment = container.WorldEnergySegmentDatastore.GetEnergySegment(removedElectricPole);
            var electricPoles = FindElectricPoleFromPeripheralService.Find(
                x, y, poleConfig, container.WorldBlockDatastore);
            
            if (electricPoles.Count != 1)
            {
                throw new Exception("周辺の電柱が1つではありません");
            }
            
            
            //セグメントから電柱の接続状態を解除
            removedSegment.RemoveEnergyTransformer(removedElectricPole);

            //周辺の機械、発電機を取得
            var (blocks, generators) =
                FindMachineAndGeneratorFromPeripheralService.Find(x, y, poleConfig, container.WorldBlockDatastore);

            //周辺の機械、発電機を接続状態から解除する
            blocks.ForEach(removedSegment.RemoveEnergyConsumer);
            generators.ForEach(removedSegment.RemoveGenerator);


            //繋がっていた1つの電柱の周辺の機械と発電機を探索
            var (connectedX, connectedY) = container.WorldBlockDatastore.GetBlockPosition(electricPoles[0].EntityId);
            var connectedPoleConfig =
                container.BlockConfig.GetBlockConfig(((IBlock) electricPoles[0]).BlockId).Param as ElectricPoleConfigParam;
            var (connectedBlocks, connectedGenerators) =
                FindMachineAndGeneratorFromPeripheralService.Find(connectedX, connectedY, connectedPoleConfig, container.WorldBlockDatastore);

            //セグメントに追加する
            connectedBlocks.ForEach(removedSegment.AddEnergyConsumer);
            connectedGenerators.ForEach(removedSegment.AddGenerator);
        }
    }
}