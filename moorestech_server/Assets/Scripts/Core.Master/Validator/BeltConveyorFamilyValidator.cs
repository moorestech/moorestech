using System;
using System.Collections.Generic;
using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Validator
{
    /// <summary>
    /// beltConveyorFamilies定義がコード側の解決規則を満たすか検証する
    /// Validates that beltConveyorFamilies satisfy the code-side resolution rules
    /// </summary>
    public static class BeltConveyorFamilyValidator
    {
        public static string Validate(Blocks blocks)
        {
            // Initialize前の検証用にGUIDから要素を引く
            // Build a GUID lookup for validation before Initialize
            var elementByGuid = new Dictionary<Guid, BlockMasterElement>();
            foreach (var block in blocks.Data) elementByGuid[block.BlockGuid] = block;

            var logs = "";
            var seenMemberGuids = new HashSet<Guid>();
            foreach (var family in blocks.BeltConveyorFamilies) logs += ValidateFamily(family);

            // すべてのベルトをいずれか1ファミリーへ所属させる
            // Require every belt block to belong to one family
            foreach (var block in blocks.Data)
            {
                if (!IsBeltBlock(block)) continue;
                if (!seenMemberGuids.Contains(block.BlockGuid))
                    logs += $"[BlockMaster] BeltConveyor block {block.Name} belongs to no beltConveyorFamily\n";
            }

            return logs;

            #region Internal

            string ValidateFamily(BeltConveyorFamiliesElement family)
            {
                var familyLogs = "";

                // 直線は必須、坂は任意として同じメンバー規則を検証する
                // Validate the required straight and optional slopes with one member rule
                familyLogs += ValidateMember(family.StraightBlockGuid, "straightBlockGuid");
                familyLogs += ValidateOptionalMember(family.UpBlockGuid, "upBlockGuid");
                familyLogs += ValidateOptionalMember(family.DownBlockGuid, "downBlockGuid");
                return familyLogs;
            }

            string ValidateOptionalMember(Guid? blockGuid, string fieldName)
            {
                return blockGuid.HasValue ? ValidateMember(blockGuid.Value, fieldName) : "";
            }

            string ValidateMember(Guid blockGuid, string fieldName)
            {
                var memberLogs = "";
                if (!TryResolve(blockGuid, fieldName, out var block, ref memberLogs)) return memberLogs;
                ValidateMembership(blockGuid, block, ref memberLogs);

                // ファミリーメンバーはすべて1セルに限定する
                // Restrict every family member to one cell
                if (block.BlockSize.x != 1 || block.BlockSize.y != 1 || block.BlockSize.z != 1)
                    memberLogs += $"[BlockMaster] BeltConveyorFamily member {block.Name} blockSize must be [1,1,1]\n";
                return memberLogs;
            }

            bool TryResolve(Guid blockGuid, string fieldName, out BlockMasterElement block, ref string outLogs)
            {
                if (elementByGuid.TryGetValue(blockGuid, out block)) return true;
                outLogs += $"[BlockMaster] BeltConveyorFamily has invalid {fieldName}:{blockGuid}\n";
                return false;
            }

            // ベルト系のみを許可し、多重所属を同時に記録する
            // Allow only belt blocks while recording duplicate membership
            void ValidateMembership(Guid blockGuid, BlockMasterElement block, ref string outLogs)
            {
                if (!IsBeltBlock(block))
                    outLogs += $"[BlockMaster] BeltConveyorFamily member {block.Name} is not a belt block (blockType:{block.BlockType})\n";
                if (!seenMemberGuids.Add(blockGuid))
                    outLogs += $"[BlockMaster] BeltConveyor block {block.Name} belongs to more than one family\n";
            }

            bool IsBeltBlock(BlockMasterElement block)
            {
                return block.BlockType == BlockMasterElement.BlockTypeConst.BeltConveyor ||
                       block.BlockType == BlockMasterElement.BlockTypeConst.GearBeltConveyor;
            }

            #endregion
        }
    }
}
