using Core.BeltTransport;
using Game.Block.Interface;

namespace Game.Block.Blocks.BeltConveyor.Transport.Rebuild
{
    // 再構築前の内部segment上のアイテム。合流マスのblockと入力方向で持ち、同じ組の内部segmentが再び作られる場合だけ引き継ぐ
    // Items on an internal segment before a rebuild, kept by the merge block and input direction; carried over only when the same pair is created again
    public readonly struct BeltInternalItemRecord
    {
        public readonly BlockInstanceId MergeBlockInstanceId;
        public readonly BeltDirection InputDirection;
        // 出口に近い順。距離は内部segmentの出口基準
        // In exit order, with distances measured from the internal segment's exit
        public readonly BeltItemState[] States;

        public BeltInternalItemRecord(BlockInstanceId mergeBlockInstanceId, BeltDirection inputDirection, BeltItemState[] states)
        {
            MergeBlockInstanceId = mergeBlockInstanceId;
            InputDirection = inputDirection;
            States = states;
        }
    }
}
