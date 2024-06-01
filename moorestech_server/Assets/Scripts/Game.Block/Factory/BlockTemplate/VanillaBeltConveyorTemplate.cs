using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Factory.Extension;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Component;
using Game.Context;
using UnityEngine;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaBeltConveyorTemplate : IBlockTemplate
    {
        public const string SlopeUpBeltConveyor = "gear belt conveyor up";
        public const string SlopeDownBeltConveyor = "gear belt conveyor down";
        public const string Hueru = "gear belt conveyor hueru";
        public const string Kieru = "gear belt conveyor kieru";

        public IBlock New(BlockConfigData config, int entityId, BlockPositionInfo blockPositionInfo)
        {
            var beltParam = config.Param as BeltConveyorConfigParam;
            var blockName = ServerContext.BlockConfig.GetBlockConfig(config.BlockHash).Name;

            var connectorComponent = config.CreateConnector(blockPositionInfo);
            var beltComponent = new VanillaBeltConveyorComponent(beltParam.BeltConveyorItemNum, beltParam.TimeOfItemEnterToExit, connectorComponent, blockName);
            var components = new List<IBlockComponent>
            {
                beltComponent,
                connectorComponent,
            };

            return new BlockSystem(entityId, config.BlockId, components, blockPositionInfo);
        }

        public IBlock Load(string state, BlockConfigData config, int entityId, BlockPositionInfo blockPositionInfo)
        {
            //TODo UP bletからの入力を受付?
            var beltParam = config.Param as BeltConveyorConfigParam;

            var blockName = config.Name;

            var connectorComponent = config.CreateConnector(blockPositionInfo);
            var beltComponent = new VanillaBeltConveyorComponent(state, beltParam.BeltConveyorItemNum, beltParam.TimeOfItemEnterToExit, connectorComponent, blockName);
            var components = new List<IBlockComponent>
            {
                beltComponent,
                connectorComponent,
            };

            return new BlockSystem(entityId, config.BlockId, components, blockPositionInfo);
        }
    }
}