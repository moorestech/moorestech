using System;
using Game.Block.Config.LoadConfig.Param;
using Game.Context;
using Game.EnergySystem;

namespace Game.World.EventHandler.EnergyEvent.EnergyService
{
    /// <summary>
    ///     TODo ここのイベントハンドラを全部削除する
    /// </summary>
    /// <typeparam name="TSegment"></typeparam>
    /// <typeparam name="TConsumer"></typeparam>
    /// <typeparam name="TGenerator"></typeparam>
    /// <typeparam name="TTransformer"></typeparam>
    public static class DisconnectOneElectricPoleFromSegmentService<TSegment, TConsumer, TGenerator, TTransformer>
        where TSegment : EnergySegment, new()
        where TConsumer : IElectricConsumer
        where TGenerator : IElectricGenerator
        where TTransformer : IElectricTransformer
    {
        public static void Disconnect(IElectricTransformer removedElectricPole, EnergyServiceDependencyContainer<TSegment> container)
        {
            //必要なデータを取得
            var pos = ServerContext.WorldBlockDatastore.GetBlockPosition(removedElectricPole.EntityId);
            var removedBlock = ServerContext.WorldBlockDatastore.GetBlock(pos);
            var poleConfig = ServerContext.BlockConfig.GetBlockConfig(removedBlock.BlockId).Param as ElectricPoleConfigParam;
            var removedSegment = container.WorldEnergySegmentDatastore.GetEnergySegment(removedElectricPole);
            var electricPoles = FindElectricPoleFromPeripheralService.Find(
                pos, poleConfig);
            
            if (electricPoles.Count != 1) throw new Exception("周辺の電柱が1つではありません");
            
            
            //セグメントから電柱の接続状態を解除
            removedSegment.RemoveEnergyTransformer(removedElectricPole);
            
            //周辺の機械、発電機を取得
            (var blocks, var generators) =
                FindMachineAndGeneratorFromPeripheralService.Find(pos, poleConfig);
            
            //周辺の機械、発電機を接続状態から解除する
            blocks.ForEach(removedSegment.RemoveEnergyConsumer);
            generators.ForEach(removedSegment.RemoveGenerator);
            
            
            //繋がっていた1つの電柱の周辺の機械と発電機を探索
            var connectedPos = ServerContext.WorldBlockDatastore.GetBlockPosition(electricPoles[0].EntityId);
            var connectedBlock = ServerContext.WorldBlockDatastore.GetBlock(connectedPos);
            var connectedPoleConfig = ServerContext.BlockConfig.GetBlockConfig(connectedBlock.BlockId).Param as ElectricPoleConfigParam;
            (var connectedBlocks, var connectedGenerators) =
                FindMachineAndGeneratorFromPeripheralService.Find(connectedPos, connectedPoleConfig);
            
            //セグメントに追加する
            connectedBlocks.ForEach(removedSegment.AddEnergyConsumer);
            connectedGenerators.ForEach(removedSegment.AddGenerator);
        }
    }
}