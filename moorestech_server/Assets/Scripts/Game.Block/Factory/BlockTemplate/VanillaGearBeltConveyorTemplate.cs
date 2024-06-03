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

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaGearBeltConveyorTemplate : IBlockTemplate
    {
        public IBlock New(BlockConfigData config, int entityId, BlockPositionInfo blockPositionInfo)
        {
            var gearBeltConveyorConfigParam = config.Param as GearBeltConveyorConfigParam;
            var blockName = ServerContext.BlockConfig.GetBlockConfig(config.BlockHash).Name;
            
            BlockConnectorComponent<IBlockInventory> connector = config.CreateConnector(blockPositionInfo);
            var vanillaBeltConveyorComponent = new VanillaBeltConveyorComponent(
                gearBeltConveyorConfigParam!.BeltConveyorItemNum,
                gearBeltConveyorConfigParam!.TimeOfItemEnterToExit,
                connector,
                blockName
            );
            var gearBeltConveyorComponent = new GearBeltConveyorComponent(vanillaBeltConveyorComponent, gearBeltConveyorConfigParam.RequiredTorque, connector);
            
            var blockComponents = new List<IBlockComponent>
            {
                gearBeltConveyorComponent,
                vanillaBeltConveyorComponent,
                connector,
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