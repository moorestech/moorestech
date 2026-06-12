using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.ItemsModule;

namespace Core.Master.Validator
{
    public static class ItemMasterUtil
    {
        public static bool Validate(Items items, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += ModuleParamValidation();
            errorLogs += LevelItemGuidsValidation();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string ModuleParamValidation()
            {
                // tier は1以上、effectValue / tradeoffValue は0以上であることを検証
                // Validate that tier is at least 1 and effectValue / tradeoffValue are non-negative
                var logs = "";
                foreach (var item in items.Data)
                {
                    var moduleParam = item.ModuleParam;
                    if (moduleParam == null) continue;

                    if (moduleParam.Tier < 1)
                    {
                        logs += $"[ItemMaster] Name:{item.Name} has invalid module Tier:{moduleParam.Tier} (must be >= 1)\n";
                    }
                    if (moduleParam.EffectValue < 0)
                    {
                        logs += $"[ItemMaster] Name:{item.Name} has invalid module EffectValue:{moduleParam.EffectValue} (must be >= 0)\n";
                    }
                    if (moduleParam.TradeoffValue < 0)
                    {
                        logs += $"[ItemMaster] Name:{item.Name} has invalid module TradeoffValue:{moduleParam.TradeoffValue} (must be >= 0)\n";
                    }
                }

                return logs;
            }

            string LevelItemGuidsValidation()
            {
                // 各レベル変種GUIDが実在し、自分自身や重複を含まないことを検証
                // Validate that every level variant GUID exists and that the list contains neither the item itself nor duplicates
                var logs = "";
                var allItemGuids = items.Data.Select(item => item.ItemGuid).ToHashSet();
                foreach (var item in items.Data)
                {
                    if (item.LevelItemGuids == null) continue;

                    if (item.LevelItemGuids.Length == 0)
                    {
                        logs += $"[ItemMaster] Name:{item.Name} has empty levelItemGuids\n";
                    }

                    var levelGuids = new HashSet<Guid>();
                    foreach (var levelItemGuid in item.LevelItemGuids)
                    {
                        if (!allItemGuids.Contains(levelItemGuid))
                        {
                            logs += $"[ItemMaster] Name:{item.Name} has invalid level ItemGuid:{levelItemGuid}\n";
                        }
                        if (levelItemGuid == item.ItemGuid)
                        {
                            logs += $"[ItemMaster] Name:{item.Name} levelItemGuids must not contain the item itself\n";
                        }
                        if (!levelGuids.Add(levelItemGuid))
                        {
                            logs += $"[ItemMaster] Name:{item.Name} has duplicate level ItemGuid:{levelItemGuid}\n";
                        }
                    }
                }

                return logs;
            }

            #endregion
        }

        public static void Initialize(
            Items items,
            out Dictionary<ItemId, ItemMasterElement> itemElementTableById,
            out Dictionary<Guid, ItemId> itemGuidToItemId,
            out Dictionary<ItemId, ItemId[]> levelVariantTable)
        {
            // ソート優先度、GUIDの順番でソート
            // Sort by SortPriority, then by GUID
            var sortedItemElements = items.Data.ToList().
                OrderBy(x => x.SortPriority ?? float.MaxValue).
                ThenBy(x => x.ItemGuid).
                ToList();

            // アイテムID 0は空のアイテムとして予約しているので、1から始める
            // Item ID 0 is reserved for empty item, so start from 1
            itemElementTableById = new Dictionary<ItemId, ItemMasterElement>();
            itemGuidToItemId = new Dictionary<Guid, ItemId>();
            for (var i = 0; i < sortedItemElements.Count; i++)
            {
                var itemId = new ItemId(i + 1);
                itemElementTableById.Add(itemId, sortedItemElements[i]);
                itemGuidToItemId.Add(sortedItemElements[i].ItemGuid, itemId);
            }

            // レベルファミリーのテーブルを構築（レベル1は基準アイテム自身、以降はlevelItemGuidsの定義順）
            // Build the level family table (level 1 is the base item itself, followed by levelItemGuids in order)
            levelVariantTable = new Dictionary<ItemId, ItemId[]>();
            foreach (var (itemId, element) in itemElementTableById)
            {
                if (element.LevelItemGuids == null || element.LevelItemGuids.Length == 0) continue;

                var variants = new ItemId[element.LevelItemGuids.Length + 1];
                variants[0] = itemId;
                for (var i = 0; i < element.LevelItemGuids.Length; i++)
                {
                    variants[i + 1] = itemGuidToItemId[element.LevelItemGuids[i]];
                }
                levelVariantTable.Add(itemId, variants);
            }
        }
    }
}
