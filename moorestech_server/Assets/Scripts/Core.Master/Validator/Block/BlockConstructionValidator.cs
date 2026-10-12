using System;
using System.Collections.Generic;
using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Validator.Block
{
    internal static class BlockConstructionValidator
    {
        internal static string ValidateRequiredItems(Blocks blocks)
        {
            // itemGuid実在性+重複を検証
            // Validate itemGuid existence and reject duplicates within a block's requiredItems
            var logs = "";
            foreach (var block in blocks.Data)
            {
                if (block.RequiredItems == null) continue;

                var seenItemGuids = new HashSet<Guid>();
                foreach (var requiredItem in block.RequiredItems)
                {
                    var id = MasterHolder.ItemMaster.GetItemIdOrNull(requiredItem.ItemGuid);
                    if (id == null)
                    {
                        logs += $"[BlockMaster] Name:{block.Name} has invalid RequiredItem.ItemGuid:{requiredItem.ItemGuid}\n";
                    }

                    // ConstructionCostServiceは重複を合算しないため、重複定義はマスタエラーとする
                    // ConstructionCostService does not sum duplicates, so a duplicate definition is a master error
                    if (!seenItemGuids.Add(requiredItem.ItemGuid))
                    {
                        logs += $"[BlockMaster] Name:{block.Name} has duplicate RequiredItem.ItemGuid:{requiredItem.ItemGuid}\n";
                    }

                    // count 0以下は無償設置と0個返却を生むためマスタエラー
                    // Non-positive counts allow free placement and zero-stack refunds, so treat them as master errors
                    if (requiredItem.Count <= 0)
                    {
                        logs += $"[BlockMaster] Name:{block.Name} has invalid RequiredItem.Count:{requiredItem.Count}\n";
                    }
                }
            }

            return logs;
        }

        internal static string ValidatePlacementsPerCost(Blocks blocks)
        {
            // 0以下は設置ごとの消費が定義できないためマスタエラー
            // Non-positive values cannot define per-placement consumption, so treat them as master errors
            var logs = "";
            foreach (var block in blocks.Data)
            {
                if (block.PlacementsPerCost <= 0)
                    logs += $"[BlockMaster] Name:{block.Name} has invalid PlacementsPerCost:{block.PlacementsPerCost}\n";

                // 財布方式が働くには消費対象の素材が要る。RequiredItemsが空だと「財布が肩代わりした」と「そもそも消費が無い」が区別できなくなるためマスタエラー
                // The wallet mechanism needs items to consume; an empty RequiredItems would make "wallet covered it" indistinguishable from "nothing to consume", so treat it as a master error
                if (1 < block.PlacementsPerCost && (block.RequiredItems == null || block.RequiredItems.Length == 0))
                    logs += $"[BlockMaster] Name:{block.Name} has PlacementsPerCost:{block.PlacementsPerCost} but no RequiredItems\n";
            }
            return logs;
        }
    }
}
