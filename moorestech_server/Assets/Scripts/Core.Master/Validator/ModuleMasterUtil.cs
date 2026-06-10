using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.ModulesModule;

namespace Core.Master.Validator
{
    public static class ModuleMasterUtil
    {
        public static bool Validate(Modules modules, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += ItemGuidValidation();
            errorLogs += DuplicateValidation();
            errorLogs += RangeValidation();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string ItemGuidValidation()
            {
                // itemGuid が ItemMaster に存在することを検証
                // Validate that each module's itemGuid exists in ItemMaster
                var logs = "";
                foreach (var module in modules.Data)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(module.ItemGuid);
                    if (itemId == null)
                    {
                        logs += $"[ModuleMaster] Name:{module.Name} has invalid ItemGuid:{module.ItemGuid}\n";
                    }
                }

                return logs;
            }

            string DuplicateValidation()
            {
                // moduleGuid / itemGuid が重複していないことを検証
                // Validate that moduleGuid / itemGuid are not duplicated
                var logs = "";
                var moduleGuids = new HashSet<Guid>();
                var itemGuids = new HashSet<Guid>();
                foreach (var module in modules.Data)
                {
                    if (!moduleGuids.Add(module.ModuleGuid))
                    {
                        logs += $"[ModuleMaster] Name:{module.Name} has duplicate ModuleGuid:{module.ModuleGuid}\n";
                    }
                    if (!itemGuids.Add(module.ItemGuid))
                    {
                        logs += $"[ModuleMaster] Name:{module.Name} has duplicate ItemGuid:{module.ItemGuid}\n";
                    }
                }

                return logs;
            }

            string RangeValidation()
            {
                // tier は1以上、effectValue / tradeoffValue は0以上であることを検証
                // Validate that tier is at least 1 and effectValue / tradeoffValue are non-negative
                var logs = "";
                foreach (var module in modules.Data)
                {
                    if (module.Tier < 1)
                    {
                        logs += $"[ModuleMaster] Name:{module.Name} has invalid Tier:{module.Tier} (must be >= 1)\n";
                    }
                    if (module.EffectValue < 0)
                    {
                        logs += $"[ModuleMaster] Name:{module.Name} has invalid EffectValue:{module.EffectValue} (must be >= 0)\n";
                    }
                    if (module.TradeoffValue < 0)
                    {
                        logs += $"[ModuleMaster] Name:{module.Name} has invalid TradeoffValue:{module.TradeoffValue} (must be >= 0)\n";
                    }
                }

                return logs;
            }

            #endregion
        }

        public static void Initialize(
            Modules modules,
            out Dictionary<Guid, ModuleMasterElement> itemGuidTable)
        {
            // Dictionary構築（Validate成功後に実行）
            // Build the dictionary (executed after Validate succeeds)
            itemGuidTable = modules.Data.ToDictionary(x => x.ItemGuid, x => x);
        }
    }
}
