using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Component;
using Game.Block.Blocks.BeltConveyor.Connection;
using Game.Block.Blocks.Connector;
using Game.Block.Blocks.Service;
using Game.Block.Blocks.ElectricWire;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate.SpaceElevator
{
    public class VanillaSpaceElevatorBaseTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            var param = (SpaceElevatorBaseBlockParam)blockMasterElement.BlockParam;
            var connector = BlockTemplateUtil.CreateInventoryConnector(param.InventoryConnectors, blockPositionInfo);
            var inventory = new SpaceElevatorBaseInventoryComponent(blockInstanceId, param, new ConnectingInventoryListPriorityInsertItemService(blockInstanceId, connector));
            var climber = new SpaceElevatorClimberComponent(param, inventory);
            return Assemble(null, blockMasterElement, blockInstanceId, blockPositionInfo, connector, inventory, climber);
        }

        public IBlock Load(Dictionary<string, object> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var param = (SpaceElevatorBaseBlockParam)blockMasterElement.BlockParam;
            var connector = BlockTemplateUtil.CreateInventoryConnector(param.InventoryConnectors, blockPositionInfo);
            var inventory = new SpaceElevatorBaseInventoryComponent(componentStates, blockInstanceId, param, new ConnectingInventoryListPriorityInsertItemService(blockInstanceId, connector));
            var climber = new SpaceElevatorClimberComponent(componentStates, param, inventory);
            return Assemble(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo, connector, inventory, climber);
        }

        private static IBlock Assemble(Dictionary<string, object> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo,
            BlockConnectorComponent<IBlockInventory, BeltInventoryConnectionContext> connector, SpaceElevatorBaseInventoryComponent inventory, SpaceElevatorClimberComponent climber)
        {
            var param = (SpaceElevatorBaseBlockParam)blockMasterElement.BlockParam;
            var electric = new SpaceElevatorBaseElectricComponent(blockInstanceId, climber);
            var wireConnector = new ElectricWireConnectorComponent(param.MaxWireConnectionCount, blockInstanceId, electric, componentStates);

            // 供給の受け取り(electric)を昇降機(climber)より前に並べる（前例 VanillaElectricPumpTemplate）
            // Order the supply receiver before the climber (precedent: VanillaElectricPumpTemplate)
            var components = new List<IBlockComponent> { connector, inventory, electric, wireConnector, climber };
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
