using System.Linq;
using Mooresmaster.Model.BuildMenuModule;

namespace Core.Master.Validator
{
    public static class BuildToolMasterUtil
    {
        public static bool Validate(BuildToolMasterElement[] buildTools, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += ValidateDuplicateGuid();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string ValidateDuplicateGuid()
            {
                // buildToolGuidの重複を検出する
                // Detect duplicate buildToolGuid
                var logs = "";
                foreach (var duplicated in buildTools.GroupBy(b => b.BuildToolGuid).Where(g => 1 < g.Count()))
                {
                    logs += $"[BuildToolMaster] duplicate BuildToolGuid:{duplicated.Key}\n";
                }

                return logs;
            }

            #endregion
        }
    }
}
