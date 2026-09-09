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

        public bool IsSlopeBlock(BlockId blockId)
        {
            return UpBlockId.HasValue && blockId == UpBlockId.Value ||
                   DownBlockId.HasValue && blockId == DownBlockId.Value;
        }
    }
}
