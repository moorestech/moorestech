using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Gear;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Fluid;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaSteamGearGeneratorTemplate : IBlockTemplate
    {
        public VanillaSteamGearGeneratorTemplate()
        {
        }
        
        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return CreateSteamGearGenerator(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] initializeParams = null)
        {
            return CreateSteamGearGenerator(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        private IBlock CreateSteamGearGenerator(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var configParam = blockMasterElement.BlockParam as SteamGearGeneratorBlockParam;
            
            // ギア接続の設定
            var gearConnectSetting = configParam.Gear.GearConnects;
            var gearConnectorComponent = new BlockConnectorComponent<IGearEnergyTransformer>(gearConnectSetting, gearConnectSetting, blockPositionInfo);
            
            // アイテム接続の設定
            var inventoryConnector = BlockTemplateUtil.CreateInventoryConnector(configParam.InventoryConnectors, blockPositionInfo);

            // アイテムインベントリコンポーネント
            var itemComponent = componentStates == null
                ? new SteamGearGeneratorItemComponent(configParam, blockInstanceId)
                : new SteamGearGeneratorItemComponent(componentStates, configParam, blockInstanceId);
            
            // 流体接続の設定
            var fluidConnector = IFluidInventory.CreateFluidInventoryConnector(configParam.FluidInventoryConnectors, blockPositionInfo);
            
            // SteamGearGeneratorFluidComponentの作成（fluidConnectorを渡す）
            var fluidComponent = componentStates == null
                ? new SteamGearGeneratorFluidComponent(
                    configParam.FluidCapacity,
                    fluidConnector
                )
                : new SteamGearGeneratorFluidComponent(
                    componentStates,
                    configParam.FluidCapacity,
                    fluidConnector
                );
            
            // スチームギアジェネレータコンポーネント
            var steamGearGeneratorComponent = componentStates == null 
                ? new SteamGearGeneratorComponent(
                    configParam, 
                    blockInstanceId, 
                    gearConnectorComponent,
                    itemComponent,
                    fluidComponent
                )
                : new SteamGearGeneratorComponent(
                    componentStates,
                    configParam, 
                    blockInstanceId, 
                    gearConnectorComponent,
                    itemComponent,
                    fluidComponent
                );
            
            var components = new List<IBlockComponent>
            {
                steamGearGeneratorComponent,
                gearConnectorComponent,
                inventoryConnector,
                itemComponent,
                fluidConnector,
                fluidComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
