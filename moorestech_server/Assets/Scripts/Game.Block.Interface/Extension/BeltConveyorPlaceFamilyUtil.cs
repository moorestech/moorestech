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

        // 財布・解放の代表。坂ロールだけ直線へ寄せ、直線・分岐器・ファミリー外は自身
        // Wallet/unlock representative: only slope roles map to the straight; straight, splitter and non-members stay themselves
        public static BlockId ResolveSlopeRepresentativeBlockId(BlockId blockId)
        {
            if (!TryGetFamily(blockId, out var family)) return blockId;
            if (!family.TryGetRole(blockId, out var role)) return blockId;
            return role == BeltConveyorRole.Up || role == BeltConveyorRole.Down ? family.StraightBlockId : blockId;
        }

        public static Guid ResolveSlopeRepresentativeGuid(Guid blockGuid)
        {
            var blockId = MasterHolder.BlockMaster.GetBlockIdOrNull(blockGuid);
            if (blockId == null) return blockGuid;
            var representative = ResolveSlopeRepresentativeBlockId(blockId.Value);
            return MasterHolder.BlockMaster.GetBlockMaster(representative).BlockGuid;
        }

        private static bool IsMember(BeltConveyorFamiliesElement element, Guid blockGuid)
        {
            return element.StraightBlockGuid == blockGuid ||
                   element.UpBlockGuid == blockGuid ||
                   element.DownBlockGuid == blockGuid ||
                   element.SplitterBlockGuid == blockGuid;
        }

        // ファミリーのGUIDを実行時IDへ解決する
        // Resolve the family's GUIDs to runtime IDs
        private static BeltConveyorFamily BuildFamily(BeltConveyorFamiliesElement element)
        {
            var straightBlockId = MasterHolder.BlockMaster.GetBlockId(element.StraightBlockGuid);
            return new BeltConveyorFamily(straightBlockId, ResolveOptional(element.UpBlockGuid), ResolveOptional(element.DownBlockGuid), ResolveOptional(element.SplitterBlockGuid));
        }

        private static BlockId? ResolveOptional(Guid? blockGuid)
        {
            if (blockGuid == null) return null;
            return MasterHolder.BlockMaster.GetBlockId(blockGuid.Value);
        }
    }
}
