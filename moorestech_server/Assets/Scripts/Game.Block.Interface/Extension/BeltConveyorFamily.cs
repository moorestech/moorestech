using Core.Master;

namespace Game.Block.Interface.Extension
{
    /// <summary>
    /// 解決済みのベルトファミリー（1ティア）。直線は必須、坂・分岐器は任意
    /// A resolved belt family (one tier); straight is required, slopes and splitter are optional
    /// </summary>
    public class BeltConveyorFamily
    {
        public readonly BlockId StraightBlockId;
        public readonly BlockId? UpBlockId;
        public readonly BlockId? DownBlockId;
        public readonly BlockId? SplitterBlockId;

        public BeltConveyorFamily(BlockId straightBlockId, BlockId? upBlockId, BlockId? downBlockId, BlockId? splitterBlockId)
        {
            StraightBlockId = straightBlockId;
            UpBlockId = upBlockId;
            DownBlockId = downBlockId;
            SplitterBlockId = splitterBlockId;
        }

        // メンバーのロールを引く。非メンバーはfalseで、outはStraight（有効値）が入るため戻り値を必ず確認すること
        // Resolve a member's role; non-members return false with out set to Straight (a valid value), so always check the return value
        public bool TryGetRole(BlockId blockId, out BeltConveyorRole role)
        {
            if (blockId == StraightBlockId)
            {
                role = BeltConveyorRole.Straight;
                return true;
            }

            if (Matches(UpBlockId, blockId))
            {
                role = BeltConveyorRole.Up;
                return true;
            }

            if (Matches(DownBlockId, blockId))
            {
                role = BeltConveyorRole.Down;
                return true;
            }

            if (Matches(SplitterBlockId, blockId))
            {
                role = BeltConveyorRole.Splitter;
                return true;
            }

            role = BeltConveyorRole.Straight;
            return false;
        }

        // ロールに対応するブロックを引く。ファミリーがそのロールを持たなければfalse
        // Resolve the block for a role; false when the family lacks that role
        public bool TryGetBlockIdOfRole(BeltConveyorRole role, out BlockId blockId)
        {
            BlockId? candidate = role switch
            {
                BeltConveyorRole.Straight => StraightBlockId,
                BeltConveyorRole.Up => UpBlockId,
                BeltConveyorRole.Down => DownBlockId,
                BeltConveyorRole.Splitter => SplitterBlockId,
                _ => null,
            };
            blockId = candidate ?? default;
            return candidate.HasValue;
        }

        // 坂ブロックなら上下どちらの坂かを返す
        // Returns which way the slope goes when the block is a slope
        public bool TryGetSlopeDirection(BlockId blockId, out BlockVerticalDirection verticalDirection)
        {
            if (TryGetRole(blockId, out var role))
            {
                if (role == BeltConveyorRole.Up)
                {
                    verticalDirection = BlockVerticalDirection.Up;
                    return true;
                }

                if (role == BeltConveyorRole.Down)
                {
                    verticalDirection = BlockVerticalDirection.Down;
                    return true;
                }
            }

            verticalDirection = BlockVerticalDirection.Horizontal;
            return false;
        }

        private static bool Matches(BlockId? member, BlockId blockId)
        {
            return member.HasValue && member.Value == blockId;
        }
    }
}
