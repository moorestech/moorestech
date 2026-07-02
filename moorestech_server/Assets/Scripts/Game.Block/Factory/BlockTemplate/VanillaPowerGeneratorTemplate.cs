using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.ElectricWire;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaPowerGeneratorTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return GetBlock(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return GetBlock(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        private IBlock GetBlock(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var param = (ElectricGeneratorBlockParam)blockMasterElement.BlockParam;

            var inventoryConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(param.InventoryConnectors, blockPositionInfo);
            var fluidConnectorComponent = IFluidInventory.CreateFluidInventoryConnector(param.FluidInventoryConnectors, blockPositionInfo);

            var generatorComponent = componentStates == null
                ? new VanillaElectricGeneratorComponent(blockInstanceId, blockPositionInfo, param)
                : new VanillaElectricGeneratorComponent(componentStates, blockInstanceId, blockPositionInfo, param);

            // 発電機はGenerator役をワイヤー端点に渡す
            // Generator passes the generator role to the wire endpoint
            var wireConnector = new ElectricWireConnectorComponent(param.MaxWireConnectionCount, param.MaxWireLength, blockInstanceId, null, generatorComponent, null, componentStates);

            var components = new List<IBlockComponent>
            {
                generatorComponent,
                inventoryConnectorComponent,
                fluidConnectorComponent,
                wireConnector,
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
