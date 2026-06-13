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
            // アイテムGUID集合は両バリデーションで参照するため先に構築する
            // Build the item GUID set once; both validations reference it
            var allItemGuids = items.Data.Select(item => item.ItemGuid).ToHashSet();

            errorLogs = "";
            errorLogs += ModulesValidation();
            errorLogs += LevelFamiliesValidation();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string ModulesValidation()
            {
                // itemGuidの実在、moduleGuid/itemGuidの重複、tier・効果値の範囲を検証
                // Validate itemGuid existence, moduleGuid/itemGuid duplicates, and tier/value ranges
                var logs = "";
                if (items.Modules == null) return logs;

                var moduleGuids = new HashSet<Guid>();
                var moduleItemGuids = new HashSet<Guid>();
                foreach (var module in items.Modules)
                {
                    if (!allItemGuids.Contains(module.ItemGuid))
                    {
                        logs += $"[ItemMaster.modules] Name:{module.Name} has invalid ItemGuid:{module.ItemGuid}\n";
                    }
                    if (!moduleGuids.Add(module.ModuleGuid))
                    {
                        logs += $"[ItemMaster.modules] Name:{module.Name} has duplicate ModuleGuid:{module.ModuleGuid}\n";
                    }
                    if (!moduleItemGuids.Add(module.ItemGuid))
                    {
                        logs += $"[ItemMaster.modules] Name:{module.Name} has duplicate ItemGuid:{module.ItemGuid}\n";
                    }
                    if (module.Tier < 1)
                    {
                        logs += $"[ItemMaster.modules] Name:{module.Name} has invalid Tier:{module.Tier} (must be >= 1)\n";
                    }
                    if (module.EffectValue < 0)
                    {
                        logs += $"[ItemMaster.modules] Name:{module.Name} has invalid EffectValue:{module.EffectValue} (must be >= 0)\n";
                    }
                    if (module.TradeoffValue < 0)
                    {
                        logs += $"[ItemMaster.modules] Name:{module.Name} has invalid TradeoffValue:{module.TradeoffValue} (must be >= 0)\n";
                    }
                }

                return logs;
            }

            string LevelFamiliesValidation()
            {
                // baseItemGuid/各変種の実在、空配列・baseItemGuid重複・先頭=base・変種重複を検証
                // Validate existence, empty arrays, baseItemGuid duplicates, level-1=base, and variant duplicates
                var logs = "";
                if (items.LevelFamilies == null) return logs;

                var baseGuids = new HashSet<Guid>();
                foreach (var family in items.LevelFamilies)
                {
                    if (!allItemGuids.Contains(family.BaseItemGuid))
                    {
                        logs += $"[ItemMaster.levelFamilies] Name:{family.Name} has invalid BaseItemGuid:{family.BaseItemGuid}\n";
                    }
                    if (!baseGuids.Add(family.BaseItemGuid))
                    {
                        logs += $"[ItemMaster.levelFamilies] Name:{family.Name} has duplicate BaseItemGuid:{family.BaseItemGuid}\n";
                    }
                    if (family.LevelItemGuids.Length == 0)
                    {
                        logs += $"[ItemMaster.levelFamilies] Name:{family.Name} has empty levelItemGuids\n";
                    }

                    // レベル1（先頭）は基準アイテム自身であることを検証
                    // Validate that level 1 (the first entry) is the base item itself
                    if (0 < family.LevelItemGuids.Length && family.LevelItemGuids[0] != family.BaseItemGuid)
                    {
                        logs += $"[ItemMaster.levelFamilies] Name:{family.Name} levelItemGuids[0]:{family.LevelItemGuids[0]} must equal BaseItemGuid:{family.BaseItemGuid}\n";
                    }

                    var levelGuids = new HashSet<Guid>();
                    foreach (var levelItemGuid in family.LevelItemGuids)
                    {
                        if (!allItemGuids.Contains(levelItemGuid))
                        {
                            logs += $"[ItemMaster.levelFamilies] Name:{family.Name} has invalid level ItemGuid:{levelItemGuid}\n";
                        }
                        if (!levelGuids.Add(levelItemGuid))
                        {
                            logs += $"[ItemMaster.levelFamilies] Name:{family.Name} has duplicate level ItemGuid:{levelItemGuid}\n";
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
            out Dictionary<ItemId, ModuleMasterElement> moduleByItemId,
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

            // 装着アイテムId→モジュール定義のテーブルを構築（root modules由来）
            // Build the equipped itemId → module definition table (from root modules)
            moduleByItemId = new Dictionary<ItemId, ModuleMasterElement>();
            if (items.Modules != null)
            {
                foreach (var module in items.Modules)
                {
                    moduleByItemId.Add(itemGuidToItemId[module.ItemGuid], module);
                }
            }

            // 基準ItemId→レベル順変種配列のテーブルを構築（先頭=基準自身）
            // Build the baseItemId → level-ordered variant table (index 0 = the base)
            levelVariantTable = new Dictionary<ItemId, ItemId[]>();
            if (items.LevelFamilies != null)
            {
                foreach (var family in items.LevelFamilies)
                {
                    var baseItemId = itemGuidToItemId[family.BaseItemGuid];

                    // outパラメータはlambdaで捕捉できないため明示ループで変換する
                    // Convert with an explicit loop since out params cannot be captured in a lambda
                    var variants = new ItemId[family.LevelItemGuids.Length];
                    for (var i = 0; i < family.LevelItemGuids.Length; i++)
                    {
                        variants[i] = itemGuidToItemId[family.LevelItemGuids[i]];
                    }
                    levelVariantTable.Add(baseItemId, variants);
                }
            }
        }
    }
}
