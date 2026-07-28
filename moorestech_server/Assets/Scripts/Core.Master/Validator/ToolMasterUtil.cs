using System.Linq;
using Mooresmaster.Model.ItemsModule;

namespace Core.Master.Validator
{
    public static class ToolMasterUtil
    {
        public static bool Validate(ToolMasterElement[] tools, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += ValidateToolItemGuid();
            errorLogs += ValidateDuplicateToolItemGuid();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string ValidateToolItemGuid()
            {
                // toolItemGuidが実在するかを検証する
                // Validate that each toolItemGuid actually exists
                var logs = "";
                foreach (var element in tools)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(element.ToolItemGuid);
                    if (itemId == null)
                    {
                        logs += $"[ToolMaster] has invalid ToolItemGuid:{element.ToolItemGuid}\n";
                    }
                }

                return logs;
            }

            string ValidateDuplicateToolItemGuid()
            {
                // toolItemGuidの重複を検出する
                // Detect duplicate toolItemGuid
                var logs = "";
                foreach (var duplicated in tools.GroupBy(t => t.ToolItemGuid).Where(g => 1 < g.Count()))
                {
                    logs += $"[ToolMaster] duplicate ToolItemGuid:{duplicated.Key}\n";
                }

                return logs;
            }

            #endregion
        }
    }
}
