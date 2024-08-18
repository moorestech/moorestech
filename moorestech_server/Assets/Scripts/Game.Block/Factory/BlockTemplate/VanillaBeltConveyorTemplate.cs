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
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaBeltConveyorTemplate : IBlockTemplate
    {
        public const string SlopeUpBeltConveyor = "gear belt conveyor up";
        public const string SlopeDownBeltConveyor = "gear belt conveyor down";
        public const string Hueru = "gear belt conveyor hueru";
        public const string Kieru = "gear belt conveyor kieru";
        
        public IBlock New(BlockElement blockElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var beltParam = blockElement.Param as BeltConveyor;
            var blockName = ServerContext.BlockConfig.GetBlockConfig(config.BlockHash).Name;
            
            BlockConnectorComponent<IBlockInventory> connectorComponent = config.CreateInventoryConnector(blockPositionInfo);
            var beltComponent = new VanillaBeltConveyorComponent(beltParam.BeltConveyorItemNum, beltParam.TimeOfItemEnterToExit, connectorComponent, blockName);
            var components = new List<IBlockComponent>
            {
                beltComponent,
                connectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, config.BlockId, components, blockPositionInfo);
        }
        
        public IBlock Load(string state, BlockConfigData config, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            //TODo UP bletからの入力を受付?
            var beltParam = config.Param as BeltConveyorConfigParam;
            
            var blockName = config.Name;
            
            BlockConnectorComponent<IBlockInventory> connectorComponent = config.CreateInventoryConnector(blockPositionInfo);
            var beltComponent = new VanillaBeltConveyorComponent(state, beltParam.BeltConveyorItemNum, beltParam.TimeOfItemEnterToExit, connectorComponent, blockName);
            var components = new List<IBlockComponent>
            {
                beltComponent,
                connectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, config.BlockId, components, blockPositionInfo);
        }
    }
}