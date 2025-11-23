using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaGearMachineTemplate : IBlockTemplate
    {
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdateEvent;
        
        public VanillaGearMachineTemplate(BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;
        }
        
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
            var machineParam = blockMasterElement.BlockParam as GearMachineBlockParam;
            BlockConnectorComponent<IBlockInventory> inventoryConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(machineParam.InventoryConnectors, blockPositionInfo);
            
            var blockId = MasterHolder.BlockMaster.GetBlockId(blockMasterElement.BlockGuid);
            var (input, output) = BlockTemplateUtil.GetMachineIOInventory(blockId, blockInstanceId, machineParam, inventoryConnectorComponent, _blockInventoryUpdateEvent);
            
            var connectSetting = machineParam.Gear.GearConnects;
            var gearConnector = new BlockConnectorComponent<IGearEnergyTransformer>(connectSetting, connectSetting, blockPositionInfo);
            var requiredTorque = new Torque(machineParam.RequireTorque);
            var overloadConfig = GearOverloadConfig.From(machineParam);
            var gearEnergyTransformer = new GearEnergyTransformer(requiredTorque, blockInstanceId, gearConnector);
            
            var requirePower = new ElectricPower(machineParam.RequireTorque * machineParam.RequiredRpm);
            
            // パラメーターをロードするか、新規作成する
            // Load the parameters or create new ones
            var processor = componentStates == null ? new VanillaMachineProcessorComponent(input, output, null, requirePower) : BlockTemplateUtil.MachineLoadState(componentStates, input, output, requirePower, blockMasterElement);
            
            var blockInventory = new VanillaMachineBlockInventoryComponent(input, output);
            var machineSave = new VanillaMachineSaveComponent(input, output, processor);
            
            var machineComponent = new VanillaGearMachineComponent(processor, gearEnergyTransformer, machineParam);
            
            var components = new List<IBlockComponent>
            {
                blockInventory,
                machineSave,
                processor,
                machineComponent,
                inventoryConnectorComponent,
                gearConnector,
                gearEnergyTransformer,
            };
            
            // 過負荷破壊コンポーネントを追加
            // Add overload breakage component
            if (overloadConfig.IsActive)
            {
                components.Add(new GearOverloadBreakageComponent(blockInstanceId, gearEnergyTransformer, overloadConfig));
            }
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
