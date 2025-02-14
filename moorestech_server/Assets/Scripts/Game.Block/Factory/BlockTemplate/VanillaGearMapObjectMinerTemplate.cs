using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.Chest;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.MapObjectMiner;
using Game.Block.Blocks.Service;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaGearMapObjectMinerTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return GetBlock(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return GetBlock(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        private IBlock GetBlock(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var minerParam = blockMasterElement.BlockParam as GearMapObjectMinerBlockParam;
            
            
            // チェストの作成
            // Create a chest
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(minerParam.InventoryConnectors, blockPositionInfo);
            var inserter = new ConnectingInventoryListPriorityInsertItemService(inputConnectorComponent);
            var chestComponent = componentStates == null ?
                new VanillaChestComponent(blockInstanceId, minerParam.ItemSlotCount, inserter) :
                new VanillaChestComponent(componentStates, blockInstanceId, minerParam.ItemSlotCount, inserter);
            
            // 歯車の接続に必要なコンポーネント
            // Components required for gear connection
            var gearConnectSetting = minerParam.Gear.GearConnects;
            var gearConnector = new BlockConnectorComponent<IGearEnergyTransformer>(gearConnectSetting, gearConnectSetting, blockPositionInfo);
            var gearEnergyTransformer = new GearEnergyTransformer(new Torque(minerParam.RequireTorque), blockInstanceId, gearConnector);
            
            // MapObject採掘機
            // MapObject Miner
            var gearMapObjectMinerProcessorComponent = componentStates == null ?
                new VanillaGearMapObjectMinerProcessorComponent(blockPositionInfo, minerParam, chestComponent) :
                new VanillaGearMapObjectMinerProcessorComponent(componentStates, blockPositionInfo, minerParam, chestComponent);
            
            var gearMinerComponent = new VanillaGearMapObjectMinerComponent(gearEnergyTransformer, minerParam, gearMapObjectMinerProcessorComponent);
            
            var components = new List<IBlockComponent>
            {
                chestComponent,
                inputConnectorComponent,
                gearMapObjectMinerProcessorComponent,
                gearConnector,
                gearEnergyTransformer,
                gearMinerComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}