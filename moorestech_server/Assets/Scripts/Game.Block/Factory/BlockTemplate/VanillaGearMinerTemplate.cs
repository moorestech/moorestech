using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.Miner;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaGearMinerTemplate : IBlockTemplate
    {
        private readonly BlockOpenableInventoryUpdateEvent _blockOpenableInventoryUpdateEvent;
        
        public VanillaGearMinerTemplate(BlockOpenableInventoryUpdateEvent blockOpenableInventoryUpdateEvent)
        {
            _blockOpenableInventoryUpdateEvent = blockOpenableInventoryUpdateEvent;
        }
        
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams = null)
        {
            return GetBlock(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return GetBlock(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        private IBlock GetBlock(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var minerParam = blockMasterElement.BlockParam as GearMinerBlockParam;
            var miningSettings = minerParam.MineSettings;
            
            var connectSetting = minerParam.Gear.GearConnects;
            var gearConnector = new BlockConnectorComponent<IGearEnergyTransformer>(connectSetting, connectSetting, blockPositionInfo);
            var gearEnergyTransformer = new GearEnergyTransformer(new Torque(minerParam.RequireTorque), blockInstanceId, gearConnector);
            
            var requestPower = new ElectricPower(minerParam.RequireTorque * minerParam.RequiredRpm);
            var outputSlot = minerParam.OutputItemSlotCount;
            var inventoryConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(minerParam.InventoryConnectors, blockPositionInfo);
            var minerProcessorComponent = componentStates == null ? 
                new VanillaMinerProcessorComponent(blockInstanceId, requestPower, outputSlot, _blockOpenableInventoryUpdateEvent, inventoryConnectorComponent, blockPositionInfo, miningSettings) : 
                new VanillaMinerProcessorComponent(componentStates, blockInstanceId, requestPower, outputSlot, _blockOpenableInventoryUpdateEvent, inventoryConnectorComponent, blockPositionInfo, miningSettings);
                
            var gearMinerComponent = new VanillaGearMinerComponent(minerProcessorComponent, gearEnergyTransformer, minerParam);
            
            var components = new List<IBlockComponent>
            {
                minerProcessorComponent,
                inventoryConnectorComponent,
                gearConnector,
                gearEnergyTransformer,
                gearMinerComponent,
                
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}