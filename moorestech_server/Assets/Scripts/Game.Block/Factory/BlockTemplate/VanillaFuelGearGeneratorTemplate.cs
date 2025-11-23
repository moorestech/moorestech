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
    public class VanillaFuelGearGeneratorTemplate : IBlockTemplate
    {
        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return CreateFuelGearGenerator(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return CreateFuelGearGenerator(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        private IBlock CreateFuelGearGenerator(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var configParam = blockMasterElement.BlockParam as FuelGearGeneratorBlockParam;
            var overloadConfig = GearOverloadConfig.From(configParam);
            
            // ギア接続の設定
            var gearConnectSetting = configParam.Gear.GearConnects;
            var gearConnectorComponent = new BlockConnectorComponent<IGearEnergyTransformer>(gearConnectSetting, gearConnectSetting, blockPositionInfo);
            
            // アイテム接続の設定
            var inventoryConnector = BlockTemplateUtil.CreateInventoryConnector(configParam.InventoryConnectors, blockPositionInfo);

            // アイテムインベントリコンポーネント
            var itemComponent = componentStates == null
                ? new FuelGearGeneratorItemComponent(configParam, blockInstanceId)
                : new FuelGearGeneratorItemComponent(componentStates, configParam, blockInstanceId);
            
            // 流体接続の設定
            var fluidConnector = IFluidInventory.CreateFluidInventoryConnector(configParam.FluidInventoryConnectors, blockPositionInfo);
            
            // FuelGearGeneratorFluidComponentの作成（fluidConnectorを渡す）
            var fluidComponent = componentStates == null
                ? new FuelGearGeneratorFluidComponent(
                    configParam.FluidCapacity,
                    fluidConnector
                )
                : new FuelGearGeneratorFluidComponent(
                    componentStates,
                    configParam.FluidCapacity,
                    fluidConnector
                );
            
            // 燃料ギアジェネレータコンポーネント
            // Configure the fuel gear generator aggregate component
            var fuelGearGeneratorComponent = componentStates == null 
                ? new FuelGearGeneratorComponent(
                    configParam, 
                    blockInstanceId, 
                    gearConnectorComponent,
                    itemComponent,
                    fluidComponent
                )
                : new FuelGearGeneratorComponent(
                    componentStates,
                    configParam, 
                    blockInstanceId, 
                    gearConnectorComponent,
                    itemComponent,
                    fluidComponent
                );
            
            var components = new List<IBlockComponent>
            {
                fuelGearGeneratorComponent,
                gearConnectorComponent,
                inventoryConnector,
                itemComponent,
                fluidConnector,
                fluidComponent,
            };
            
            // 過負荷破壊コンポーネントを追加
            // Add overload breakage component
            if (overloadConfig.IsActive)
            {
                components.Add(new GearOverloadBreakageComponent(blockInstanceId, fuelGearGeneratorComponent, overloadConfig));
            }
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
