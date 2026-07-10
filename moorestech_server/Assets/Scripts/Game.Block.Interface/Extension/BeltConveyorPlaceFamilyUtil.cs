using System;
using System.Collections.Generic;
using Core.Master;
using Mooresmaster.Model.PlaceSystemModule;

namespace Game.Block.Interface.Extension
{
    /// <summary>
    /// placeSystemマスターのBeltConveyorエントリからファミリー（代表・斜面・長尺）を解決する
    /// Resolves belt conveyor families (representative, slopes, length variants) from BeltConveyor placeSystem entries
    /// </summary>
    public static class BeltConveyorPlaceFamilyUtil
    {
        public static bool TryGetFamily(BlockId blockId, out BeltConveyorPlaceParam beltParam)
        {
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockGuid;
            return TryGetFamilyByGuid(blockGuid, out beltParam);
        }

        public static bool TryGetFamilyByGuid(Guid blockGuid, out BeltConveyorPlaceParam beltParam)
        {
            // 全BeltConveyorエントリを走査しメンバー照合（エントリ数は少数のためキャッシュ不要）
            // Scan all BeltConveyor entries for membership (few entries, no cache needed)
            foreach (var element in MasterHolder.PlaceSystemMaster.PlaceSystem.Data)
            {
                if (element.PlaceParam is not BeltConveyorPlaceParam param) continue;
                if (param.UpBlockGuid == blockGuid || param.DownBlockGuid == blockGuid)
                {
                    beltParam = param;
                    return true;
                }

                foreach (var straightBlock in param.StraightBlocks)
                {
                    if (straightBlock.BlockGuid != blockGuid) continue;
                    beltParam = param;
                    return true;
                }
            }

            beltParam = null;
            return false;
        }

        public static BlockId GetRepresentativeBlockId(BeltConveyorPlaceParam beltParam)
        {
            // 代表はマスター定義length==1の直線ブロック（バリデータが1件のみを保証）
            // The representative is the master-defined length-1 straight block (validator guarantees exactly one)
            foreach (var straightBlock in beltParam.StraightBlocks)
            {
                if (straightBlock.Length == 1) return MasterHolder.BlockMaster.GetBlockId(straightBlock.BlockGuid);
            }

            throw new InvalidOperationException("BeltConveyor entry has no length-1 straight block");
        }

        public static List<(int length, BlockId blockId)> GetStraightVariantsDesc(BeltConveyorPlaceParam beltParam)
        {
            // 長さはマスター定義のlengthをそのまま使用する（blockSizeからは導出しない）
            // Lengths come straight from the master-defined length field (never derived from blockSize)
            var variants = new List<(int length, BlockId blockId)>();
            foreach (var straightBlock in beltParam.StraightBlocks)
            {
                variants.Add((straightBlock.Length, MasterHolder.BlockMaster.GetBlockId(straightBlock.BlockGuid)));
            }

            variants.Sort((a, b) => b.length.CompareTo(a.length));
            return variants;
        }

        public static bool IsHiddenVariant(Guid blockGuid)
        {
            if (!TryGetFamilyByGuid(blockGuid, out var beltParam)) return false;
            var representativeGuid = MasterHolder.BlockMaster.GetBlockMaster(GetRepresentativeBlockId(beltParam)).BlockGuid;
            return blockGuid != representativeGuid;
        }
    }
}
