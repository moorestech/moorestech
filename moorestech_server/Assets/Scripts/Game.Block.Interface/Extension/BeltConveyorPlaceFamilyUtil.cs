using System;
using System.Collections.Generic;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Interface.Extension
{
    /// <summary>
    /// beltConveyorFamilies定義からファミリーを解決するドメイン層util
    /// Domain-layer util resolving belt families from beltConveyorFamilies
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
            // 全ファミリーエントリを走査しメンバー照合。エントリ数は少数のためキャッシュ不要
            // Scan all family entries for membership; few entries so no cache is needed
            foreach (var element in MasterHolder.BlockMaster.Blocks.BeltConveyorFamilies)
            {
                if (!IsMember(element, blockGuid)) continue;
                family = BuildFamily(element);
                return true;
            }

            family = null;
            return false;
        }

        // 非代表バリアント（長尺・斜面）かを判定。非ベルトはfalse
        // Whether a block is a non-representative variant; non-belt returns false
        public static bool IsHiddenVariant(Guid blockGuid)
        {
            return TryGetFamilyByGuid(blockGuid, out var family) && family.IsHiddenVariant(MasterHolder.BlockMaster.GetBlockId(blockGuid));
        }

        private static bool IsMember(BeltConveyorFamiliesElement element, Guid blockGuid)
        {
            if (element.UpBlockGuid == blockGuid || element.DownBlockGuid == blockGuid) return true;
            foreach (var straight in element.StraightBlocks)
            {
                if (straight.BlockGuid == blockGuid) return true;
            }

            return false;
        }

        // ファミリーエントリを解決する。長さはblockSize.zから導出
        // Resolve a family entry; length is derived from blockSize.z
        private static BeltConveyorFamily BuildFamily(BeltConveyorFamiliesElement element)
        {
            var straightVariants = new List<(int length, BlockId blockId)>();
            foreach (var straight in element.StraightBlocks)
            {
                var blockId = MasterHolder.BlockMaster.GetBlockId(straight.BlockGuid);
                straightVariants.Add((MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockSize.z, blockId));
            }

            // 長い順に並べ、貪欲割当が最長バリアントから選べるようにする
            // Sort descending so greedy assignment can pick the longest variant first
            straightVariants.Sort((a, b) => b.length.CompareTo(a.length));

            var upBlockId = ResolveSlope(element.UpBlockGuid);
            var downBlockId = ResolveSlope(element.DownBlockGuid);
            return new BeltConveyorFamily(FindRepresentativeBlockId(straightVariants), straightVariants, upBlockId, downBlockId);
        }

        private static BlockId? ResolveSlope(Guid? slopeBlockGuid)
        {
            if (slopeBlockGuid == null) return null;
            return MasterHolder.BlockMaster.GetBlockId(slopeBlockGuid.Value);
        }

        // 代表は長さ1の直線ブロック（BeltConveyorFamilyValidatorがちょうど1件を保証する）
        // The representative is the length-1 straight block (BeltConveyorFamilyValidator guarantees exactly one)
        private static BlockId FindRepresentativeBlockId(List<(int length, BlockId blockId)> straightVariants)
        {
            foreach (var variant in straightVariants)
            {
                if (variant.length == 1) return variant.blockId;
            }

            throw new InvalidOperationException("BeltConveyorFamily has no length-1 straight block");
        }
    }
}
