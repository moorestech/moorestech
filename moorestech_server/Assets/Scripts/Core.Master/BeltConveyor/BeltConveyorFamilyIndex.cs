using System;
using System.Collections.Generic;
using Mooresmaster.Model.BlocksModule;

namespace Core.Master
{
    /// <summary>
    /// ブロックマスタからベルトコンベアファミリーを組み立て、ブロックIDでの逆引きを提供する
    /// Builds belt conveyor families from the block master and provides blockId reverse lookup
    /// </summary>
    public class BeltConveyorFamilyIndex
    {
        private readonly Dictionary<BlockId, BeltConveyorFamily> _familyByBlockId = new();

        public BeltConveyorFamilyIndex(Blocks blocks, Dictionary<Guid, BlockId> blockGuidToBlockId)
        {
            // ファミリー名でベルトブロックを束ねる。役割（代表・長尺・斜面）はslopeTypeとblockSizeから決まる
            // Group belt blocks by family name; roles (representative/length/slope) come from slopeType and blockSize
            var membersByFamilyName = new Dictionary<string, List<BlockMasterElement>>();
            foreach (var block in blocks.Data)
            {
                if (block.BlockParam is not IBeltConveyorFamilyParam beltParam) continue;
                if (!membersByFamilyName.TryGetValue(beltParam.BeltConveyorFamily, out var members))
                {
                    members = new List<BlockMasterElement>();
                    membersByFamilyName[beltParam.BeltConveyorFamily] = members;
                }

                members.Add(block);
            }

            foreach (var (familyName, members) in membersByFamilyName)
            {
                var family = CreateFamily(familyName, members);
                foreach (var member in members) _familyByBlockId[blockGuidToBlockId[member.BlockGuid]] = family;
            }

            #region Internal

            BeltConveyorFamily CreateFamily(string familyName, List<BlockMasterElement> members)
            {
                var straightVariants = new List<(int length, BlockId blockId)>();
                BlockId? upBlockId = null;
                BlockId? downBlockId = null;
                var memberBlockIds = new HashSet<BlockId>();

                foreach (var member in members)
                {
                    var blockId = blockGuidToBlockId[member.BlockGuid];
                    memberBlockIds.Add(blockId);

                    // 斜面は1マス固定、直線は長さ違いのバリアントになる
                    // Slopes are always one cell; straights become length variants
                    switch (((IBeltConveyorFamilyParam)member.BlockParam).SlopeType)
                    {
                        case BeltConveyorBlockParam.SlopeTypeConst.Up:
                            upBlockId = blockId;
                            break;
                        case BeltConveyorBlockParam.SlopeTypeConst.Down:
                            downBlockId = blockId;
                            break;
                        default:
                            straightVariants.Add((member.BlockSize.z, blockId));
                            break;
                    }
                }

                // 長い順に並べ、貪欲割当が最長バリアントから選べるようにする
                // Sort descending so greedy assignment can pick the longest variant first
                straightVariants.Sort((a, b) => b.length.CompareTo(a.length));

                return new BeltConveyorFamily(familyName, FindRepresentativeBlockId(familyName, straightVariants), straightVariants, upBlockId, downBlockId, memberBlockIds);
            }

            BlockId FindRepresentativeBlockId(string familyName, List<(int length, BlockId blockId)> straightVariants)
            {
                // 代表は長さ1の直線ブロック（バリデータがちょうど1件を保証する）
                // The representative is the length-1 straight block (the validator guarantees exactly one)
                foreach (var variant in straightVariants)
                {
                    if (variant.length == 1) return variant.blockId;
                }

                throw new InvalidOperationException($"BeltConveyorFamily {familyName} has no length-1 straight block");
            }

            #endregion
        }

        public bool TryGetFamily(BlockId blockId, out BeltConveyorFamily family)
        {
            return _familyByBlockId.TryGetValue(blockId, out family);
        }
    }
}
