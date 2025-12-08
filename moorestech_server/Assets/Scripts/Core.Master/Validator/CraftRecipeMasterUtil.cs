using System;
using System.Collections.Generic;
using Mooresmaster.Model.CraftRecipesModule;

namespace Core.Master.Validator
{
    public static class CraftRecipeMasterUtil
    {
        public static bool Validate(CraftRecipes craftRecipes, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += ValidateCraftRecipe();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string ValidateCraftRecipe()
            {
                // チェックするアイテムのGUIDを取得
                // Get item GUIDs to check
                var checkTargets = new List<Guid>();
                foreach (var craftRecipeMasterElement in craftRecipes.Data)
                {
                    foreach (var requiredItem in craftRecipeMasterElement.RequiredItems)
                    {
                        checkTargets.Add(requiredItem.ItemGuid);
                    }
                    checkTargets.Add(craftRecipeMasterElement.CraftResultItemGuid);
                }

                // アイテムのGUIDが存在するかチェック
                // Check if item GUIDs exist
                var logs = "";
                foreach (var checkItem in checkTargets)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(checkItem);
                    if (itemId == null)
                    {
                        logs += $"[CraftRecipeMaster] has invalid ItemGuid:{checkItem}\n";
                    }
                }

                return logs;
            }

            #endregion
        }

        public static void Initialize(CraftRecipes craftRecipes)
        {
            // CraftRecipeMasterは追加の初期化処理がないため、空実装
            // CraftRecipeMaster has no additional initialization, so empty implementation
        }
    }
}
