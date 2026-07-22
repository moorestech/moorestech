using System;
using System.Collections.Generic;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.BuildMenuModule;

namespace Core.Master.Validator
{
    /// <summary>
    /// buildMenuのreplaceFamilies定義がコード側の解決規則を満たすか検証する
    /// Validates that buildMenu replaceFamilies satisfy the code-side resolution rules
    /// </summary>
    public static class ReplaceFamilyValidator
    {
        public static string Validate(Blocks blocks, ReplaceFamilyElement[] replaceFamilies)
        {
            // ブロックGUIDの実在確認用の索引を構築する
            // Build an index to verify block GUID existence
            var existingBlockGuids = new HashSet<Guid>();
            foreach (var block in blocks.Data) existingBlockGuids.Add(block.BlockGuid);

            var logs = "";
            var assignedFamilyByBlockGuid = new Dictionary<Guid, string>();
            foreach (var family in replaceFamilies)
            foreach (var target in family.TargetBlocks)
            {
                // foreignKeyは自動生成されないため参照先の実在を手動で確認する
                // foreignKey validation is not auto-generated, so verify the referenced block exists
                if (!existingBlockGuids.Contains(target.BlockGuid))
                {
                    logs += $"[BlockMaster] ReplaceFamily:{family.FamilyName} has invalid BlockGuid:{target.BlockGuid}\n";
                }

                // 同一ファミリー判定は1ブロック1ファミリー前提。重複すると判定が定義順依存になるため弾く
                // Same-family judgement assumes one family per block; duplicates make it definition-order dependent
                if (assignedFamilyByBlockGuid.TryGetValue(target.BlockGuid, out var existingFamily))
                {
                    logs += $"[BlockMaster] BlockGuid:{target.BlockGuid} is assigned to multiple replace families ({existingFamily}, {family.FamilyName})\n";
                }
                else
                {
                    assignedFamilyByBlockGuid.Add(target.BlockGuid, family.FamilyName);
                }
            }

            return logs;
        }
    }
}
