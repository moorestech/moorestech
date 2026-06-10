using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.LevelFamiliesModule;

namespace Core.Master.Validator
{
    public static class LevelFamilyMasterUtil
    {
        public static bool Validate(LevelFamilies levelFamilies, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += ItemGuidValidation();
            errorLogs += StructureValidation();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string ItemGuidValidation()
            {
                // baseItemGuid と各レベルの itemGuid が ItemMaster に存在することを検証
                // Validate that the baseItemGuid and every level itemGuid exist in ItemMaster
                var logs = "";
                foreach (var family in levelFamilies.Data)
                {
                    if (MasterHolder.ItemMaster.GetItemIdOrNull(family.BaseItemGuid) == null)
                    {
                        logs += $"[LevelFamilyMaster] Name:{family.Name} has invalid BaseItemGuid:{family.BaseItemGuid}\n";
                    }
                    foreach (var levelItemGuid in family.LevelItemGuids)
                    {
                        if (MasterHolder.ItemMaster.GetItemIdOrNull(levelItemGuid) == null)
                        {
                            logs += $"[LevelFamilyMaster] Name:{family.Name} has invalid level ItemGuid:{levelItemGuid}\n";
                        }
                    }
                }

                return logs;
            }

            string StructureValidation()
            {
                // レベル配列が空でないこと、baseItemGuid が重複しないことを検証
                // Validate that level arrays are non-empty and baseItemGuid values are unique
                var logs = "";
                var baseGuids = new HashSet<Guid>();
                foreach (var family in levelFamilies.Data)
                {
                    if (family.LevelItemGuids.Length == 0)
                    {
                        logs += $"[LevelFamilyMaster] Name:{family.Name} has empty levelItemGuids\n";
                    }
                    if (!baseGuids.Add(family.BaseItemGuid))
                    {
                        logs += $"[LevelFamilyMaster] Name:{family.Name} has duplicate BaseItemGuid:{family.BaseItemGuid}\n";
                    }
                }

                return logs;
            }

            #endregion
        }

        public static void Initialize(LevelFamilies levelFamilies, out Dictionary<ItemId, ItemId[]> variantTable)
        {
            // baseItemId → レベル順変種ItemId配列のテーブルを構築（Validate成功後に実行）
            // Build the baseItemId → level-ordered variant ItemId table (executed after Validate succeeds)
            variantTable = levelFamilies.Data.ToDictionary(
                family => MasterHolder.ItemMaster.GetItemId(family.BaseItemGuid),
                family => family.LevelItemGuids.Select(guid => MasterHolder.ItemMaster.GetItemId(guid)).ToArray());
        }
    }
}
