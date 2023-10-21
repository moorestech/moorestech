using System.Collections.Generic;
using Core.EnergySystem;
using Core.EnergySystem.Electric;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.World.EventHandler.Service;

namespace Game.World.EventHandler.EnergyEvent.EnergyService
{
    public static class DisconnectTwoOrMoreElectricPoleFromSegmentService<TSegment, TConsumer, TGenerator, TTransformer>
        where TSegment : EnergySegment, new()
        where TConsumer : IEnergyConsumer
        where TGenerator : IEnergyGenerator
        where TTransformer : IEnergyTransformer
    {
        public static void Disconnect(IEnergyTransformer removedElectricPole, EnergyServiceDependencyContainer<TSegment> container)
        {
            
            var removedSegment = container.WorldEnergySegmentDatastore.GetEnergySegment(removedElectricPole);

            
            var connectedElectricPoles = new List<IEnergyTransformer>();
            foreach (var onePole in removedSegment.EnergyTransformers) connectedElectricPoles.Add(onePole.Value);
            
            connectedElectricPoles.Remove(removedElectricPole);


            
            container.WorldEnergySegmentDatastore.RemoveEnergySegment(removedSegment);


            
            //1
            
            
            while (connectedElectricPoles.Count != 0)
            {
                var (newElectricPoles, newBlocks, newGenerators) =
                    GetElectricPoles(
                        connectedElectricPoles[0],
                        removedElectricPole,
                        new Dictionary<int, IEnergyTransformer>(),
                        new Dictionary<int, IBlockElectricConsumer>(),
                        new Dictionary<int, IElectricGenerator>(), container);


                
                var newElectricSegment = container.WorldEnergySegmentDatastore.CreateEnergySegment();
                foreach (var newElectric in newElectricPoles)
                {
                    newElectricSegment.AddEnergyTransformer(newElectric.Value);
                    
                    connectedElectricPoles.Remove(newElectric.Value);
                }

                foreach (var newBlock in newBlocks) newElectricSegment.AddEnergyConsumer(newBlock.Value);
                foreach (var newGenerator in newGenerators) newElectricSegment.AddGenerator(newGenerator.Value);
            }
        }

        // 
        private static (Dictionary<int, IEnergyTransformer>, Dictionary<int, IBlockElectricConsumer>, Dictionary<int, IElectricGenerator>)
            GetElectricPoles(
                IEnergyTransformer electricPole,
                IEnergyTransformer removedElectricPole,
                Dictionary<int, IEnergyTransformer> electricPoles,
                Dictionary<int, IBlockElectricConsumer> blockElectrics,
                Dictionary<int, IElectricGenerator> powerGenerators, EnergyServiceDependencyContainer<TSegment> container)
        {
            var (x, y) = container.WorldBlockDatastore.GetBlockPosition(electricPole.EntityId);
            var poleConfig =
                container.BlockConfig.GetBlockConfig(((IBlock)electricPole).BlockId).Param as ElectricPoleConfigParam;


            
            var (newBlocks, newGenerators) =
                FindMachineAndGeneratorFromPeripheralService.Find(x, y, poleConfig, container.WorldBlockDatastore);
            
            foreach (var block in newBlocks)
            {
                if (blockElectrics.ContainsKey(block.EntityId)) continue;
                blockElectrics.Add(block.EntityId, block);
            }

            foreach (var generator in newGenerators)
            {
                if (powerGenerators.ContainsKey(generator.EntityId)) continue;
                powerGenerators.Add(generator.EntityId, generator);
            }


            
            var peripheralElectricPoles = FindElectricPoleFromPeripheralService.Find(x, y, poleConfig, container.WorldBlockDatastore);
            
            peripheralElectricPoles.Remove(removedElectricPole);
            
            electricPoles.Add(electricPole.EntityId, electricPole);
            
            if (peripheralElectricPoles.Count == 0) return (electricPoles, blockElectrics, powerGenerators);


            
            foreach (var peripheralElectricPole in peripheralElectricPoles)
            {
                
                if (electricPoles.ContainsKey(peripheralElectricPole.EntityId)) continue;
                
                (electricPoles, blockElectrics, powerGenerators) =
                    GetElectricPoles(peripheralElectricPole, removedElectricPole, electricPoles, blockElectrics,
                        powerGenerators, container);
            }

            return (electricPoles, blockElectrics, powerGenerators);
        }
    }
}