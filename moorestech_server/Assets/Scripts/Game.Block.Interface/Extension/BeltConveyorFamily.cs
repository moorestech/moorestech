using System.Collections.Generic;
using Core.Master;

namespace Game.Block.Interface.Extension
{
    /// <summary>
    /// 解決済みのベルトファミリー（代表・長尺バリアント・斜面ブロック）
    /// A resolved belt family: representative, length variants and slope blocks
    /// </summary>
    public class BeltConveyorFamily
    {
        // 代表は長さ1の直線ブロック（ビルドメニューにはこれだけ）
        // The representative is the length-1 straight block (menu shows only it)
        public readonly BlockId RepresentativeBlockId;

        // 長い順の直線バリアント（長さはblockSize.zそのもの）
        // Straight variants in descending length order (length is blockSize.z itself)
        public readonly IReadOnlyList<(int length, BlockId blockId)> StraightVariantsDesc;

        // 斜面のないファミリー（分岐器）ではnull
        // Null for slope-less families (splitters)
        public readonly BlockId? UpBlockId;
        public readonly BlockId? DownBlockId;

        public BeltConveyorFamily(BlockId representativeBlockId, IReadOnlyList<(int length, BlockId blockId)> straightVariantsDesc, BlockId? upBlockId, BlockId? downBlockId)
        {
            RepresentativeBlockId = representativeBlockId;
            StraightVariantsDesc = straightVariantsDesc;
            UpBlockId = upBlockId;
            DownBlockId = downBlockId;
        }

        // 代表以外のバリアント（長尺・斜面）はビルドメニューに出さない
        // Variants other than the representative (length/slope) are hidden from the build menu
        public bool IsHiddenVariant(BlockId blockId)
        {
            return blockId != RepresentativeBlockId;
        }
    }
}
