using System.Collections.Generic;

namespace Core.Master
{
    /// <summary>
    /// 同じbeltConveyorFamilyを持つベルトコンベア群。代表・長尺バリアント・斜面ブロックで構成される
    /// A group of belt conveyors sharing beltConveyorFamily: representative, length variants and slope blocks
    /// </summary>
    public class BeltConveyorFamily
    {
        public readonly string FamilyName;

        // 代表は1マスの直線ブロック。ビルドメニューにはこれだけが並ぶ
        // The representative is the one-cell straight block; only it appears in the build menu
        public readonly BlockId RepresentativeBlockId;

        // 長い順の直線バリアント（長さはblockSize.zそのもの）
        // Straight variants in descending length order (length is blockSize.z itself)
        public readonly IReadOnlyList<(int length, BlockId blockId)> StraightVariantsDesc;

        // 斜面バリアントを持たないファミリー（分岐器など）ではnull
        // Null for families without slope variants (e.g. splitters)
        public readonly BlockId? UpBlockId;
        public readonly BlockId? DownBlockId;

        private readonly HashSet<BlockId> _memberBlockIds;

        public BeltConveyorFamily(string familyName, BlockId representativeBlockId, IReadOnlyList<(int length, BlockId blockId)> straightVariantsDesc, BlockId? upBlockId, BlockId? downBlockId, HashSet<BlockId> memberBlockIds)
        {
            FamilyName = familyName;
            RepresentativeBlockId = representativeBlockId;
            StraightVariantsDesc = straightVariantsDesc;
            UpBlockId = upBlockId;
            DownBlockId = downBlockId;
            _memberBlockIds = memberBlockIds;
        }

        public bool Contains(BlockId blockId)
        {
            return _memberBlockIds.Contains(blockId);
        }

        // 代表以外のバリアント（長尺・斜面）はビルドメニューに出さない
        // Variants other than the representative (length/slope) are hidden from the build menu
        public bool IsHiddenVariant(BlockId blockId)
        {
            return blockId != RepresentativeBlockId;
        }
    }
}
