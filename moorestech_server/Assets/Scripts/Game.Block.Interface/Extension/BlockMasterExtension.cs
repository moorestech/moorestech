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
            if (overrideBlock == null)
            {
                return blockId;
            }
            
            var up = overrideBlock.UpBlockGuid;
            if (verticalDirection is BlockVerticalDirection.Up && up.HasValue && up != Guid.Empty)
            {
                return MasterHolder.BlockMaster.GetBlockId(up.Value);
            }
            var down = overrideBlock.DownBlockGuid;
            if (verticalDirection is BlockVerticalDirection.Horizontal && down.HasValue && down != Guid.Empty)
            {
                return MasterHolder.BlockMaster.GetBlockId(down.Value);
            }
            var horizontal = overrideBlock.HorizontalBlockGuid;
            if (verticalDirection is  BlockVerticalDirection.Down && horizontal.HasValue && horizontal != Guid.Empty)
            {
                return MasterHolder.BlockMaster.GetBlockId(horizontal.Value);
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