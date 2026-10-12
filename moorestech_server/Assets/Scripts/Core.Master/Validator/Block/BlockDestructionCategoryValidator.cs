using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Validator.Block
{
    /// <summary>
    ///     破壊カテゴリ定義の検証。blockGuidの実在・複数カテゴリへの重複登録・予約キーとの衝突を弾く
    ///     Validates destruction category definitions: block guid existence, duplicate registration and collisions with reserved keys
    /// </summary>
    internal static class BlockDestructionCategoryValidator
    {
        internal static string Validate(Blocks blocks)
        {
            var logs = "";
            var definedBlockGuids = new HashSet<Guid>(blocks.Data.Select(block => block.BlockGuid));
            var assignedCategoryByBlockGuid = new Dictionary<Guid, string>();
            foreach (var category in blocks.BlockDestructionCategories)
            {
                // 接続線用の予約キーをマスタが使うと、ブロックと接続線が同じドラッグで混ざってしまう
                // A master using the connection-line key would let blocks and lines mix in one drag
                if (category.CategoryKey == BlockMaster.ConnectionLineDestructionCategory)
                {
                    logs += $"[BlockMaster] DestructionCategory uses the reserved key {BlockMaster.ConnectionLineDestructionCategory}\n";
                }

                foreach (var target in category.TargetBlocks)
                {
                    // foreignKeyは自動生成されないため参照先の実在を手動で確認する
                    // foreignKey validation is not auto-generated, so verify the referenced block exists
                    if (!definedBlockGuids.Contains(target.BlockGuid))
                    {
                        logs += $"[BlockMaster] DestructionCategory:{category.CategoryKey} has invalid BlockGuid:{target.BlockGuid}\n";
                    }

                    // 逆引きは1ブロック1カテゴリ前提。重複するとロード順で結果が変わるため弾く
                    // The reverse lookup assumes one category per block; duplicates make the result order-dependent
                    if (assignedCategoryByBlockGuid.TryGetValue(target.BlockGuid, out var existingCategory))
                    {
                        logs += $"[BlockMaster] BlockGuid:{target.BlockGuid} is assigned to multiple destruction categories ({existingCategory}, {category.CategoryKey})\n";
                    }
                    else
                    {
                        assignedCategoryByBlockGuid.Add(target.BlockGuid, category.CategoryKey);
                    }
                }
            }

            return logs;
        }
    }
}
