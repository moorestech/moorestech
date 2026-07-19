using System;
using System.Collections.Generic;
using Mooresmaster.Model.ConnectToolsModule;

namespace Core.Master.Validator
{
    public static class ConnectToolMasterUtil
    {
        public static bool Validate(ConnectTools connectTools, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += ValidateRequiredItems();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string ValidateRequiredItems()
            {
                // requiredItemsのitemGuidが実在するかを検証する
                // Validate that each requiredItems.itemGuid actually exists
                var logs = "";
                foreach (var element in connectTools.Data)
                foreach (var requiredItem in element.RequiredItems)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(requiredItem.ItemGuid);
                    if (itemId == null)
                    {
                        logs += $"[ConnectToolMaster] Name:{element.Name} has invalid ItemGuid:{requiredItem.ItemGuid}\n";
                    }
                }

                return logs;
            }

            #endregion
        }

        public static void Initialize(ConnectTools connectTools)
        {
            // ConnectToolMasterは索引構築をマスタ側で行うため、追加の初期化はなし
            // ConnectToolMaster builds its index on the master side, so no extra initialization
        }
    }
}
