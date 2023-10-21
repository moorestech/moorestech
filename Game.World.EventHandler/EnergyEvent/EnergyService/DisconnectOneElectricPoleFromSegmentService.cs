using System;
using Core.EnergySystem;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.World.EventHandler.Service;

namespace Game.World.EventHandler.EnergyEvent.EnergyService
{
    /// <summary>
    ///     TODo 
    /// </summary>
    /// <typeparam name="TSegment"></typeparam>
    /// <typeparam name="TConsumer"></typeparam>
    /// <typeparam name="TGenerator"></typeparam>
    /// <typeparam name="TTransformer"></typeparam>
    public static class DisconnectOneElectricPoleFromSegmentService<TSegment, TConsumer, TGenerator, TTransformer>
        where TSegment : EnergySegment, new()
        where TConsumer : IEnergyConsumer
        where TGenerator : IEnergyGenerator
        where TTransformer : IEnergyTransformer
    {
        public static void Disconnect(IEnergyTransformer removedElectricPole, EnergyServiceDependencyContainer<TSegment> container)
        {
            
            var (x, y) = container.WorldBlockDatastore.GetBlockPosition(removedElectricPole.EntityId);
            var poleConfig =
                container.BlockConfig.GetBlockConfig(((IBlock)removedElectricPole).BlockId).Param as ElectricPoleConfigParam;
            var removedSegment = container.WorldEnergySegmentDatastore.GetEnergySegment(removedElectricPole);
            var electricPoles = FindElectricPoleFromPeripheralService.Find(
                x, y, poleConfig, container.WorldBlockDatastore);

            if (electricPoles.Count != 1) throw new Exception("1");


            
            removedSegment.RemoveEnergyTransformer(removedElectricPole);

            
            var (blocks, generators) =
                FindMachineAndGeneratorFromPeripheralService.Find(x, y, poleConfig, container.WorldBlockDatastore);

            
            blocks.ForEach(removedSegment.RemoveEnergyConsumer);
            generators.ForEach(removedSegment.RemoveGenerator);


            //1
            var (connectedX, connectedY) = container.WorldBlockDatastore.GetBlockPosition(electricPoles[0].EntityId);
            var connectedPoleConfig =
                container.BlockConfig.GetBlockConfig(((IBlock)electricPoles[0]).BlockId).Param as ElectricPoleConfigParam;
            var (connectedBlocks, connectedGenerators) =
                FindMachineAndGeneratorFromPeripheralService.Find(connectedX, connectedY, connectedPoleConfig, container.WorldBlockDatastore);

            
            connectedBlocks.ForEach(removedSegment.AddEnergyConsumer);
            connectedGenerators.ForEach(removedSegment.AddGenerator);
        }
    }
}