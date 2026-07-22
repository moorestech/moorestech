using System;
using System.Collections.Generic;
using Mooresmaster.Model.BuildMenuModule;

namespace Core.Master.Validator
{
    public static class ConnectToolMasterUtil
    {
        public static bool Validate(ConnectToolMasterElement[] connectTools, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += ValidateRequiredItems();
            errorLogs += ValidateLengthPerUnit();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string ValidateRequiredItems()
            {
                // requiredItemsのitemGuidが実在するかを検証する
                // Validate that each requiredItems.itemGuid actually exists
                var logs = "";
                foreach (var element in connectTools)
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

            string ValidateLengthPerUnit()
            {
                // lengthPerUnitが0以下だとコスト算出で0除算になるため不正として検出する
                // Detect non-positive lengthPerUnit which would cause division-by-zero in cost calculation
                var logs = "";
                foreach (var element in connectTools)
                {
                    if (element.LengthPerUnit <= 0)
                    {
                        logs += $"[ConnectToolMaster] Name:{element.Name} has invalid LengthPerUnit:{element.LengthPerUnit} (must be greater than 0)\n";
                    }
                }

                return logs;
            }

            #endregion
        }
    }
}
