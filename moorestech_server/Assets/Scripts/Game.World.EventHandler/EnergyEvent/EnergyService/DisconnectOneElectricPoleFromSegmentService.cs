using System;
using System.Collections.Generic;
using Core.EnergySystem;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
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
            var poleConfig = ServerContext.BlockConfig.GetBlockConfig(((IBlock)removedElectricPole).BlockId).Param as ElectricPoleConfigParam;
            var removedSegment = container.WorldEnergySegmentDatastore.GetEnergySegment(removedElectricPole);
            List<IElectricTransformer> electricPoles = FindElectricPoleFromPeripheralService.Find(
                pos, poleConfig);

            if (electricPoles.Count != 1) throw new Exception("周辺の電柱が1つではありません");


            //セグメントから電柱の接続状態を解除
            removedSegment.RemoveEnergyTransformer(removedElectricPole);

            //周辺の機械、発電機を取得
            (List<IElectricConsumer> blocks, List<IElectricGenerator> generators) =
                FindMachineAndGeneratorFromPeripheralService.Find(pos, poleConfig);

            //周辺の機械、発電機を接続状態から解除する
            blocks.ForEach(removedSegment.RemoveEnergyConsumer);
            generators.ForEach(removedSegment.RemoveGenerator);


            //繋がっていた1つの電柱の周辺の機械と発電機を探索
            var connectedPos = ServerContext.WorldBlockDatastore.GetBlockPosition(electricPoles[0].EntityId);
            var connectedPoleConfig = ServerContext.BlockConfig.GetBlockConfig(((IBlock)electricPoles[0]).BlockId).Param as ElectricPoleConfigParam;
            (List<IElectricConsumer> connectedBlocks, List<IElectricGenerator> connectedGenerators) =
                FindMachineAndGeneratorFromPeripheralService.Find(connectedPos, connectedPoleConfig);

            //セグメントに追加する
            connectedBlocks.ForEach(removedSegment.AddEnergyConsumer);
            connectedGenerators.ForEach(removedSegment.AddGenerator);
        }
    }
}