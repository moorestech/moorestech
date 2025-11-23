using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.Pump;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Blocks.Fluid;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaGearPumpTemplate : IBlockTemplate
    {
        private readonly IBlockRemover _blockRemover;

        public VanillaGearPumpTemplate(IBlockRemover blockRemover)
        {
            _blockRemover = blockRemover;
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
            var param = (GearPumpBlockParam)blockMasterElement.BlockParam;
            var overloadConfig = GearOverloadConfig.From(param);

            // Gear connector and transformer
            var gearConnectSetting = param.Gear.GearConnects;
            var gearConnector = new BlockConnectorComponent<IGearEnergyTransformer>(gearConnectSetting, gearConnectSetting, blockPositionInfo);
            var gearEnergyTransformer = new GearEnergyTransformer(new Torque(param.RequireTorque), blockInstanceId, gearConnector, overloadConfig, _blockRemover);

            var fluidConnector = IFluidInventory.CreateFluidInventoryConnector(param.FluidInventoryConnectors, blockPositionInfo);
            var outputComponent = componentStates == null
                ? new PumpFluidOutputComponent(param.InnerTankCapacity, fluidConnector)
                : new PumpFluidOutputComponent(componentStates, param.InnerTankCapacity, fluidConnector);
            
            var pumpComponent = new GearPumpComponent(param, gearEnergyTransformer, outputComponent);

            var components = new List<IBlockComponent>
            {
                gearConnector,
                gearEnergyTransformer,
                fluidConnector,
                outputComponent,
                pumpComponent,
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
