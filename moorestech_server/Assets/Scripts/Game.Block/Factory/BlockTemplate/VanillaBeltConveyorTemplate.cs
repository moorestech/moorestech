using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Component.IOConnector;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Component;
using Game.Context;
using UnityEngine;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaBeltConveyorTemplate : IBlockTemplate
    {
        public IBlock New(BlockConfigData param, int entityId, long blockHash, BlockPositionInfo blockPositionInfo)
        {
            var beltParam = param.Param as BeltConveyorConfigParam;
            var blockName = ServerContext.BlockConfig.GetBlockConfig(blockHash).Name;

            var connectorComponent = CreateConnector(blockPositionInfo, blockHash);
            var beltComponent = new VanillaBeltConveyorComponent(beltParam.BeltConveyorItemNum, beltParam.TimeOfItemEnterToExit, connectorComponent, blockName);
            var components = new List<IBlockComponent>
            {
                beltComponent,
                connectorComponent,
            };

            return new BlockSystem(entityId, param.BlockId, components, blockPositionInfo);
        }

        public IBlock Load(BlockConfigData param, int entityId, long blockHash, string state, BlockPositionInfo blockPositionInfo)
        {
            var beltParam = param.Param as BeltConveyorConfigParam;
            var blockName = ServerContext.BlockConfig.GetBlockConfig(blockHash).Name;

            var connectorComponent = CreateConnector(blockPositionInfo, blockHash);
            var beltComponent = new VanillaBeltConveyorComponent(state, beltParam.BeltConveyorItemNum, beltParam.TimeOfItemEnterToExit, connectorComponent,blockName);
            var components = new List<IBlockComponent>
            {
                beltComponent,
                connectorComponent,
            };

            return new BlockSystem(entityId, param.BlockId, components, blockPositionInfo);
        }

        public const string SlopeUpBeltConveyor = "gear belt conveyor up";
        public const string SlopeDownBeltConveyor = "gear belt conveyor down";
        public const string Hueru = "gear belt conveyor hueru";
        public const string Kieru = "gear belt conveyor kieru";


        private BlockConnectorComponent<IBlockInventory> CreateConnector(BlockPositionInfo blockPositionInfo, long blockHash)
        {
            var config = ServerContext.BlockConfig.GetBlockConfig(blockHash);
            if (config.Name == SlopeUpBeltConveyor)
            {
                Debug.Log("SlopeUpBeltConveyor");
                return new BlockConnectorComponent<IBlockInventory>(blockPositionInfo);
            }
            if (config.Name == SlopeDownBeltConveyor)
            {
                Debug.Log("SlopeDownBeltConveyor");
                return new BlockConnectorComponent<IBlockInventory>(blockPositionInfo);
            }

            //TODo UP bletからの入力を受付

            return new BlockConnectorComponent<IBlockInventory>(blockPositionInfo);
        }
    }
}