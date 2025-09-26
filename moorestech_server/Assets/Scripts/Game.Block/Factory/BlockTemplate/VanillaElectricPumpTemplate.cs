using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.Pump;
using Game.Block.Blocks.Fluid;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaElectricPumpTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return CreatePump(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return CreatePump(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        private static IBlock CreatePump(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var param = (ElectricPumpBlockParam)blockMasterElement.BlockParam;

            var fluidConnector = IFluidInventory.CreateFluidInventoryConnector(param.FluidInventoryConnectors, blockPositionInfo);
            var outputComponent = componentStates == null
                ? new PumpFluidOutputComponent(param.InnerTankCapacity, fluidConnector)
                : new PumpFluidOutputComponent(componentStates, param.InnerTankCapacity, fluidConnector);
            var processorComponent = new ElectricPumpProcessorComponent(param, outputComponent);
            var electricComponent = new ElectricPumpComponent(blockInstanceId, new ElectricPower(param.RequiredPower), processorComponent);

            var components = new List<IBlockComponent>
            {
                fluidConnector,
                outputComponent,
                processorComponent,
                electricComponent,
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
