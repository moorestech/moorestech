using System;
using Core.Master;

namespace Game.Block.Interface.Extension
{
    public static class BlockMasterExtension
    {
        public static BlockId GetVerticalOverrideBlockId(this BlockId blockId,BlockVerticalDirection verticalDirection)
        {
            var blockElement = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var overrideBlock = blockElement.OverrideVerticalBlock;
            
            if (verticalDirection is BlockVerticalDirection.Up && overrideBlock.UpBlockGuid != Guid.Empty)
            {
                return MasterHolder.BlockMaster.GetBlockId(overrideBlock.UpBlockGuid);
            }
            if (verticalDirection is BlockVerticalDirection.Horizontal && overrideBlock.HorizontalBlockGuid != Guid.Empty)
            {
                return MasterHolder.BlockMaster.GetBlockId(overrideBlock.HorizontalBlockGuid);
            }
            if (verticalDirection is  BlockVerticalDirection.Down && overrideBlock.DownBlockGuid != Guid.Empty)
            {
                return MasterHolder.BlockMaster.GetBlockId(overrideBlock.DownBlockGuid);
            }
            
            return blockId;
        }
        
        public static BlockId GetVerticalOverrideBlockId(this Guid blockGuid,BlockVerticalDirection verticalDirection)
        {
            var blockId = MasterHolder.BlockMaster.GetBlockId(blockGuid);
            return blockId.GetVerticalOverrideBlockId(verticalDirection);
        }
    }
}