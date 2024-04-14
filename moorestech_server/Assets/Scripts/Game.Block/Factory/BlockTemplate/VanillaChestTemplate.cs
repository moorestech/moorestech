using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.Chest;
using Game.Block.Component;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Component;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaChestTemplate : IBlockTemplate
    {
        public IBlock New(BlockConfigData param, int entityId, long blockHash, BlockPositionInfo blockPositionInfo)
        {
            var chest = param.Param as ChestConfigParam;
            var chestComponent = new VanillaChest(entityId, chest.ChestItemNum, blockPositionInfo);
            var components = new List<IBlockComponent>
            {
                chestComponent,
            };
            
            return new BlockSystem(entityId, param.BlockId, components, blockPositionInfo);
        }

        public IBlock Load(BlockConfigData param, int entityId, long blockHash, string state, BlockPositionInfo blockPositionInfo)
        {
            var chest = param.Param as ChestConfigParam;
            var chestComponent  = new VanillaChest(state, entityId, chest.ChestItemNum, blockPositionInfo);
            var components = new List<IBlockComponent>
            {
                chestComponent,
            };
            
            return new BlockSystem(entityId, param.BlockId, components, blockPositionInfo);
        }
    }
}