using System;
using System.Collections.Generic;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Interface.Extension
{
    /// <summary>
    /// blocks.jsonのbeltConveyorFamilyからファミリー（代表・斜面・長尺）を解決するドメイン層util
    /// Domain-layer util resolving belt families (representative, slopes, length variants) from blocks.json's beltConveyorFamily
    /// </summary>
    public static class BeltConveyorPlaceFamilyUtil
    {
        public static bool TryGetFamily(BlockId blockId, out BeltConveyorFamily family)
        {
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockGuid;
            return TryGetFamilyByGuid(blockGuid, out family);
        }

        public static bool TryGetFamilyByGuid(Guid blockGuid, out BeltConveyorFamily family)
        {
            // ベルト系ブロックでなければファミリー無し。エントリ数は少数のためキャッシュ不要
            // No family for non-belt blocks; few entries so no cache is needed
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockGuid);
            if (blockMaster.BlockParam is not IBeltConveyorFamilyParam beltParam)
            {
                family = null;
                return false;
            }

            family = BuildFamily(beltParam.BeltConveyorFamily);
            return true;
        }

        #region Internal

        // ファミリー名で全ベルトブロックを束ね、slopeTypeとblockSizeから役割を確定する
        // Group all belt blocks by family name and derive roles from slopeType and blockSize
        private static BeltConveyorFamily BuildFamily(string familyName)
        {
            var straightVariants = new List<(int length, BlockId blockId)>();
            BlockId? upBlockId = null;
            BlockId? downBlockId = null;
            var memberBlockIds = new HashSet<BlockId>();

            foreach (var block in MasterHolder.BlockMaster.Blocks.Data)
            {
                if (block.BlockParam is not IBeltConveyorFamilyParam beltParam) continue;
                if (beltParam.BeltConveyorFamily != familyName) continue;

                var blockId = MasterHolder.BlockMaster.GetBlockId(block.BlockGuid);
                memberBlockIds.Add(blockId);

                // 斜面は1マス固定、直線は長さ違いのバリアントになる
                // Slopes are always one cell; straights become length variants
                switch (beltParam.SlopeType)
                {
                    case BeltConveyorBlockParam.SlopeTypeConst.Up:
                        upBlockId = blockId;
                        break;
                    case BeltConveyorBlockParam.SlopeTypeConst.Down:
                        downBlockId = blockId;
                        break;
                    default:
                        straightVariants.Add((block.BlockSize.z, blockId));
                        break;
                }
            }

            // 長い順に並べ、貪欲割当が最長バリアントから選べるようにする
            // Sort descending so greedy assignment can pick the longest variant first
            straightVariants.Sort((a, b) => b.length.CompareTo(a.length));

            return new BeltConveyorFamily(familyName, FindRepresentativeBlockId(familyName, straightVariants), straightVariants, upBlockId, downBlockId, memberBlockIds);
        }

        // 代表は長さ1の直線ブロック（BeltConveyorFamilyValidatorがちょうど1件を保証する）
        // The representative is the length-1 straight block (BeltConveyorFamilyValidator guarantees exactly one)
        private static BlockId FindRepresentativeBlockId(string familyName, List<(int length, BlockId blockId)> straightVariants)
        {
            foreach (var variant in straightVariants)
            {
                if (variant.length == 1) return variant.blockId;
            }

            throw new InvalidOperationException($"BeltConveyorFamily {familyName} has no length-1 straight block");
        }

        #endregion
    }
}
