using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks;
using Game.Block.Blocks.ElectricWire;
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
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
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
            var processorComponent = new ElectricPumpProcessorComponent(param, outputComponent, blockPositionInfo);
            var electricComponent = new ElectricPumpComponent(blockInstanceId, new ElectricPower(param.RequiredPower), param.IdlePowerRate, processorComponent);
            // ポンプはConsumer役をワイヤー端点に渡す
            // Pump passes the consumer role to the wire endpoint
            var wireConnector = new ElectricWireConnectorComponent(param.MaxWireConnectionCount, param.MaxWireLength, blockInstanceId, electricComponent, componentStates);

            // 供給読み取り(electricComponent)を生成判定(processorComponent)より先に更新させるため、この並び順を維持すること
            // Keep this order: the supply reader (electricComponent) must update before the pump processor
            var components = new List<IBlockComponent>
            {
                fluidConnector,
                outputComponent,
                electricComponent,
                processorComponent,
                wireConnector,
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
