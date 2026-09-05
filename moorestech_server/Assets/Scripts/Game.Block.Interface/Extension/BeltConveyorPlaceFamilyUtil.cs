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
