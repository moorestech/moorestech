using System;
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

        // 坂ブロックかを判定し、非ベルトはfalseにする
        // Detect slope blocks while returning false for non-belt blocks
        public static bool IsSlopeBlock(Guid blockGuid)
        {
            return TryGetFamilyByGuid(blockGuid, out var family) &&
                   family.IsSlopeBlock(MasterHolder.BlockMaster.GetBlockId(blockGuid));
        }

        private static bool IsMember(BeltConveyorFamiliesElement element, Guid blockGuid)
        {
            return element.StraightBlockGuid == blockGuid ||
                   element.UpBlockGuid == blockGuid ||
                   element.DownBlockGuid == blockGuid;
        }

        // ファミリーのGUIDを実行時IDへ解決する
        // Resolve the family's GUIDs to runtime IDs
        private static BeltConveyorFamily BuildFamily(BeltConveyorFamiliesElement element)
        {
            var straightBlockId = MasterHolder.BlockMaster.GetBlockId(element.StraightBlockGuid);
            var upBlockId = ResolveSlope(element.UpBlockGuid);
            var downBlockId = ResolveSlope(element.DownBlockGuid);
            return new BeltConveyorFamily(straightBlockId, upBlockId, downBlockId);
        }

        private static BlockId? ResolveSlope(Guid? slopeBlockGuid)
        {
            if (slopeBlockGuid == null) return null;
            return MasterHolder.BlockMaster.GetBlockId(slopeBlockGuid.Value);
        }
    }
}
