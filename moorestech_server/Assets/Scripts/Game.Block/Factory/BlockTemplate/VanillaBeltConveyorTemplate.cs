using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Component;
using Game.Block.Component.IOConnector;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Component;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaBeltConveyorTemplate : IBlockTemplate
    {
        public IBlock New(BlockConfigData param, int entityId, long blockHash, BlockPositionInfo blockPositionInfo)
        {
            var beltParam = param.Param as BeltConveyorConfigParam;
            
            var connectorComponent = CreateConnector(blockPositionInfo);
            var beltComponent = new VanillaBeltConveyorComponent(beltParam.BeltConveyorItemNum, beltParam.TimeOfItemEnterToExit, connectorComponent);
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
            
            var connectorComponent = CreateConnector(blockPositionInfo);
            var beltComponent = new VanillaBeltConveyorComponent(state, beltParam.BeltConveyorItemNum, beltParam.TimeOfItemEnterToExit, connectorComponent);
            var components = new List<IBlockComponent>
            {
                beltComponent,
                connectorComponent,
            };
                
            return new BlockSystem(entityId, param.BlockId, components, blockPositionInfo);
        }


        private BlockConnectorComponent<IBlockInventory> CreateConnector(BlockPositionInfo blockPositionInfo)
        {
            return new BlockConnectorComponent<IBlockInventory>(new IOConnectionSetting(
                // 南、西、東をからの接続を受け、アイテムをインプットする
                new ConnectDirection[] { new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                //北向きに出力する
                new ConnectDirection[] { new(1, 0, 0) },
                new[]
                {
                    VanillaBlockType.Machine, VanillaBlockType.Chest, VanillaBlockType.Generator,
                    VanillaBlockType.Miner, VanillaBlockType.BeltConveyor,
                }), blockPositionInfo);
        }
    }
}