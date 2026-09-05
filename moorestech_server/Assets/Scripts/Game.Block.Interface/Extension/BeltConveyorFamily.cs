using Core.Master;

namespace Game.Block.Interface.Extension
{
    /// <summary>
    /// 解決済みのベルトファミリー（直線・斜面ブロック）
    /// A resolved belt family containing straight and slope blocks
    /// </summary>
    public class BeltConveyorFamily
    {
        // 斜面のないファミリー（分岐器）ではnull
        // Null for slope-less families (splitters)
        public readonly BlockId StraightBlockId;
        public readonly BlockId? UpBlockId;
        public readonly BlockId? DownBlockId;

        public BeltConveyorFamily(BlockId straightBlockId, BlockId? upBlockId, BlockId? downBlockId)
        {
            StraightBlockId = straightBlockId;
            UpBlockId = upBlockId;
            DownBlockId = downBlockId;
        }

        // 坂ブロックなら上下どちらの坂かを返す
        // Returns which way the slope goes when the block is a slope
        public bool TryGetSlopeDirection(BlockId blockId, out BlockVerticalDirection verticalDirection)
        {
            if (UpBlockId.HasValue && blockId == UpBlockId.Value)
            {
                verticalDirection = BlockVerticalDirection.Up;
                return true;
            }

            if (DownBlockId.HasValue && blockId == DownBlockId.Value)
            {
                verticalDirection = BlockVerticalDirection.Down;
                return true;
            }

            verticalDirection = BlockVerticalDirection.Horizontal;
            return false;
        }
    }
}
