using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Component;
using Game.Block.Blocks.BeltConveyor.Connection;
using Game.Block.Blocks.Connector;
using Game.Block.Blocks.OrbitalCargo;
using Game.Block.Blocks.Service;
using Game.Block.Blocks.ElectricWire;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface;
using Game.Context;
using Game.Orbit;
using Game.Block.Interface.Component;
using Game.UnlockState;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate.SpaceElevator
{
    public class VanillaSpaceElevatorBaseTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            var param = (SpaceElevatorBaseBlockParam)blockMasterElement.BlockParam;
            var connector = BlockTemplateUtil.CreateInventoryConnector(param.InventoryConnectors, blockPositionInfo);
            // 選択の実体は選択部品だけへ渡し、在庫（入口）には読む口として渡す
            // Hand the concrete selection only to the selector; the inventory (inlet) receives it as the read port
            var cargoSelection = new OrbitalCargoSelectionState();
            var inventory = new SpaceElevatorBaseInventoryComponent(blockInstanceId, param, new ConnectingInventoryListPriorityInsertItemService(blockInstanceId, connector), ServerContext.GetService<IOrbitalArrivalService>(), cargoSelection);
            var selector = new OrbitalCargoSelectorComponent(cargoSelection, inventory.Inlet, ServerContext.GetService<IGameUnlockStateDataController>());
            var climber = new SpaceElevatorClimberComponent(param, inventory);
            return Assemble(null, blockMasterElement, blockInstanceId, blockPositionInfo, connector, inventory, selector, climber);
        }

        public IBlock Load(Dictionary<string, object> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var param = (SpaceElevatorBaseBlockParam)blockMasterElement.BlockParam;
            var connector = BlockTemplateUtil.CreateInventoryConnector(param.InventoryConnectors, blockPositionInfo);
            var cargoSelection = new OrbitalCargoSelectionState();
            var inventory = new SpaceElevatorBaseInventoryComponent(componentStates, blockInstanceId, param, new ConnectingInventoryListPriorityInsertItemService(blockInstanceId, connector), ServerContext.GetService<IOrbitalArrivalService>(), cargoSelection);
            // 選択は昇降機より先に読む（昇降機が購読を始める前に値を確定する）
            // Read the selection before the climber so the value is settled before it subscribes
            var selector = new OrbitalCargoSelectorComponent(componentStates, cargoSelection, inventory.Inlet, ServerContext.GetService<IGameUnlockStateDataController>());
            var climber = new SpaceElevatorClimberComponent(componentStates, param, inventory);
            return Assemble(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo, connector, inventory, selector, climber);
        }

        private static IBlock Assemble(Dictionary<string, object> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo,
            BlockConnectorComponent<IBlockInventory, BeltInventoryConnectionContext> connector, SpaceElevatorBaseInventoryComponent inventory, OrbitalCargoSelectorComponent selector, SpaceElevatorClimberComponent climber)
        {
            var param = (SpaceElevatorBaseBlockParam)blockMasterElement.BlockParam;
            var electric = new SpaceElevatorBaseElectricComponent(blockInstanceId, climber);
            var wireConnector = new ElectricWireConnectorComponent(param.MaxWireConnectionCount, blockInstanceId, electric, componentStates);

            // 供給の受け取り(electric)を昇降機(climber)より前に並べる（前例 VanillaElectricPumpTemplate）
            // Order the supply receiver before the climber (precedent: VanillaElectricPumpTemplate)
            var components = new List<IBlockComponent> { connector, inventory, selector, new OrbitalCargoBlueprintSettingsComponent(selector), electric, wireConnector, climber };
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
