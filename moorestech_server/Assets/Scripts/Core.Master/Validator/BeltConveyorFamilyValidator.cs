using System;
using System.Collections.Generic;
using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Validator
{
    /// <summary>
    /// beltConveyorFamilies定義（上り/下り/直線群）がコード側の解決規則を満たすか検証する
    /// Validates that beltConveyorFamilies (up/down/straight) satisfy the code-side resolution rules
    /// </summary>
    public static class BeltConveyorFamilyValidator
    {
        public static string Validate(Blocks blocks)
        {
            // Initialize前の検証のためID表に頼らず、blocks.Dataからguid→要素表を自前で構築する
            // Validate runs before Initialize, so build a guid->element map from blocks.Data instead of using the id table
            var elementByGuid = new Dictionary<Guid, BlockMasterElement>();
            foreach (var block in blocks.Data) elementByGuid[block.BlockGuid] = block;

            var logs = "";
            var seenMemberGuids = new HashSet<Guid>();
            foreach (var family in blocks.BeltConveyorFamilies) logs += ValidateFamily(family);
            return logs;

            #region Internal

            string ValidateFamily(BeltConveyorFamiliesElement family)
            {
                var familyLogs = "";

                // 直線群は必須。長さ1（blockSize.z==1）の代表がちょうど1件必要
                // Straight blocks are required; exactly one length-1 (blockSize.z==1) representative must exist
                var lengthOneCount = 0;
                var seenLengths = new HashSet<int>();
                foreach (var straight in family.StraightBlocks)
                {
                    if (!TryResolve(straight.BlockGuid, "straightBlocks", out var block, ref familyLogs)) continue;
                    if (!ValidateMembership(straight.BlockGuid, block, ref familyLogs)) continue;

                    // 直線ブロックは1×1×N。長さは進行方向(z)から導出するため重複禁止
                    // Straight blocks are 1x1xN; length is derived from z (travel axis), so duplicates are forbidden
                    if (block.BlockSize.x != 1 || block.BlockSize.y != 1 || block.BlockSize.z < 1)
                        familyLogs += $"[BlockMaster] BeltConveyor straight block {block.Name} blockSize must be [1,1,N]\n";
                    if (!seenLengths.Add(block.BlockSize.z))
                        familyLogs += $"[BlockMaster] BeltConveyorFamily has duplicated straight length:{block.BlockSize.z} block:{block.Name}\n";
                    if (block.BlockSize.z == 1) lengthOneCount++;
                }

                if (lengthOneCount != 1)
                    familyLogs += "[BlockMaster] BeltConveyorFamily must contain exactly one length-1 straight block\n";

                // 斜面は任意だが、あれば1マス（blockSize==[1,1,1]）でなければ経路分解できない
                // Slopes are optional; when present they must be single-cell ([1,1,1]) or the path cannot be decomposed
                familyLogs += ValidateSlope(family.UpBlockGuid, "upBlockGuid");
                familyLogs += ValidateSlope(family.DownBlockGuid, "downBlockGuid");
                return familyLogs;
            }

            string ValidateSlope(Guid? slopeBlockGuid, string fieldName)
            {
                if (slopeBlockGuid == null) return "";
                var slopeLogs = "";
                if (!TryResolve(slopeBlockGuid.Value, fieldName, out var block, ref slopeLogs)) return slopeLogs;
                if (!ValidateMembership(slopeBlockGuid.Value, block, ref slopeLogs)) return slopeLogs;
                if (block.BlockSize.x != 1 || block.BlockSize.y != 1 || block.BlockSize.z != 1)
                    slopeLogs += $"[BlockMaster] BeltConveyor slope block {block.Name} blockSize must be [1,1,1]\n";
                return slopeLogs;
            }

            bool TryResolve(Guid blockGuid, string fieldName, out BlockMasterElement block, ref string outLogs)
            {
                if (elementByGuid.TryGetValue(blockGuid, out block)) return true;
                outLogs += $"[BlockMaster] BeltConveyorFamily has invalid {fieldName}:{blockGuid}\n";
                return false;
            }

            // ベルト系ブロックのみをメンバーに許可し、同一ブロックの多重所属を禁止する
            // Only belt-type blocks may be members, and a block must not belong to more than one family
            bool ValidateMembership(Guid blockGuid, BlockMasterElement block, ref string outLogs)
            {
                var ok = true;
                if (block.BlockType != BlockMasterElement.BlockTypeConst.BeltConveyor &&
                    block.BlockType != BlockMasterElement.BlockTypeConst.GearBeltConveyor)
                {
                    outLogs += $"[BlockMaster] BeltConveyorFamily member {block.Name} is not a belt block (blockType:{block.BlockType})\n";
                    ok = false;
                }

                if (!seenMemberGuids.Add(blockGuid))
                {
                    outLogs += $"[BlockMaster] BeltConveyor block {block.Name} belongs to more than one family\n";
                    ok = false;
                }

                return ok;
            }

            #endregion
        }
    }
}
