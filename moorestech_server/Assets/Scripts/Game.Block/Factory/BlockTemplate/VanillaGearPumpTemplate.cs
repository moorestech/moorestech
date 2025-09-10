using System.Collections.Generic;
using Core.Master;
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
    public class VanillaGearPumpTemplate : IBlockTemplate
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
            var pumpParam = blockMasterElement.BlockParam as GearPumpBlockParam;
            
            // Gear Energy Components
            var gearConnectSetting = pumpParam.Gear.GearConnects;
            var gearConnector = new BlockConnectorComponent<IGearEnergyTransformer>(gearConnectSetting, gearConnectSetting, blockPositionInfo);
            var requiredTorque = new Torque(pumpParam.RequireTorque);
            var gearEnergyTransformer = new GearEnergyTransformer(requiredTorque, blockInstanceId, gearConnector);
            
            // Fluid Components
            var fluidInventoryConnects = pumpParam.FluidInventoryConnectors;
            BlockConnectorComponent<IFluidInventory> fluidConnectorComponent = IFluidInventory.CreateFluidInventoryConnector(fluidInventoryConnects, blockPositionInfo);
            
            // Internal fluid container
            var fluidContainer = new FluidContainer(pumpParam.InnerTankCapacity);
            
            // Create components
            var fluidInventoryComponent = new GearPumpFluidInventoryComponent(fluidContainer);
            var pumpComponent = new GearPumpComponent(gearEnergyTransformer, fluidContainer, pumpParam, fluidInventoryComponent);
            var fluidOutputComponent = new GearPumpFluidOutputComponent(fluidContainer, fluidConnectorComponent);
            
            var components = new List<IBlockComponent>
            {
                gearConnector,
                gearEnergyTransformer,
                fluidConnectorComponent,
                fluidInventoryComponent,
                pumpComponent,
                fluidOutputComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}