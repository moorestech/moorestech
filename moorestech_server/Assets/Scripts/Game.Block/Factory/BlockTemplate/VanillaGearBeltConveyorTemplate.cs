using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Component;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Factory.Extension;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Component;
using Game.Context;
using Game.Gear.Common;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaGearBeltConveyorTemplate : IBlockTemplate
    {
        public IBlock New(BlockConfigData config, int entityId, BlockPositionInfo blockPositionInfo)
        {
            var gearBeltConveyorConfigParam = config.Param as GearBeltConveyorConfigParam;
            var blockName = ServerContext.BlockConfig.GetBlockConfig(config.BlockHash).Name;
            
            var gearEnergyTransformerConnector = new BlockConnectorComponent<IGearEnergyTransformer>(
                gearBeltConveyorConfigParam!.GearConnectSettings,
                gearBeltConveyorConfigParam!.GearConnectSettings,
                blockPositionInfo
            );
            var vanillaBeltConveyorComponent = new VanillaBeltConveyorComponent(
                gearBeltConveyorConfigParam!.BeltConveyorItemNum,
                gearBeltConveyorConfigParam!.TimeOfItemEnterToExit,
                config.CreateConnector(blockPositionInfo),
                blockName
            );
            
            var gearBeltConveyorComponent = new GearBeltConveyorComponent(vanillaBeltConveyorComponent, entityId, gearBeltConveyorConfigParam.RequiredPower, gearEnergyTransformerConnector);
            
            var blockComponents = new List<IBlockComponent>
            {
                gearBeltConveyorComponent,
                vanillaBeltConveyorComponent,
                gearEnergyTransformerConnector,
            };
            return new BlockSystem(entityId, config.BlockId, blockComponents, blockPositionInfo);
        }
        public IBlock Load(string state, BlockConfigData config, int entityId, BlockPositionInfo blockPositionInfo)
        {
            var blockComponents = new List<IBlockComponent>();
            return new BlockSystem(entityId, config.BlockId, blockComponents, blockPositionInfo);
        }
    }
}