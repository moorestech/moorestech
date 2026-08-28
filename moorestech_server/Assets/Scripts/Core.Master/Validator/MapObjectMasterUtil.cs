using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.MapModule;

namespace Core.Master.Validator
{
    public static class MapObjectMasterUtil
    {
        public static bool Validate(Map map, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += ItemGuidValidation();
            errorLogs += EarnItemsValidation();
            errorLogs += MiningToolValidation();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string ItemGuidValidation()
            {
                var logs = "";
                foreach (var mapObjectElement in map.MapObjects)
                {
                    foreach (var earnItemsElement in mapObjectElement.EarnItems)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(earnItemsElement.ItemGuid);
                        if (itemId == null)
                        {
                            logs += $"[MapObjectMaster] Name:{mapObjectElement.MapObjectName} has invalid ItemGuid:{earnItemsElement.ItemGuid}\n";
                        }
                    }

                    var miningParam = mapObjectElement.MiningParam;
                    if (miningParam is MiningMiningParam miningMiningParam)
                    {
                        foreach (var miningTool in miningMiningParam.MiningTools)
                        {
                            var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(miningTool.ToolItemGuid);
                            if (itemId == null)
                            {
                                logs += $"[MapObjectMaster] Name:{mapObjectElement.MapObjectName} has invalid ToolItemGuid:{miningTool.ToolItemGuid}\n";
                            }
                        }
                    }
                }

                return logs;
            }

            string EarnItemsValidation()
            {
                // 取得アイテム無しは殴っても空振り
                // Empty earn items yield nothing when mined
                var logs = "";
                foreach (var mapObjectElement in map.MapObjects)
                {
                    if (mapObjectElement.EarnItems.Length == 0)
                    {
                        logs += $"[MapObjectMaster] Name:{mapObjectElement.MapObjectName} has empty EarnItems\n";
                    }
                }

                return logs;
            }

            string MiningToolValidation()
            {
                var logs = "";
                foreach (var mapObjectElement in map.MapObjects)
                {
                    if (mapObjectElement.MiningParam is not MiningMiningParam miningParam) continue;

                    foreach (var duplicated in miningParam.MiningTools.GroupBy(tool => tool.ToolItemGuid).Where(group => 1 < group.Count()))
                    {
                        logs += $"[MapObjectMaster] Name:{mapObjectElement.MapObjectName} has duplicate ToolItemGuid:{duplicated.Key}\n";
                    }
                    foreach (var miningTool in miningParam.MiningTools)
                    {
                        if (miningTool.Damage <= 0)
                            logs += $"[MapObjectMaster] Name:{mapObjectElement.MapObjectName} has non-positive Damage:{miningTool.Damage}\n";
                        if (miningTool.AttackSpeed <= 0)
                            logs += $"[MapObjectMaster] Name:{mapObjectElement.MapObjectName} has non-positive AttackSpeed:{miningTool.AttackSpeed}\n";
                    }
                }

                return logs;
            }

            #endregion
        }

        public static void Initialize(Map map, out Dictionary<Guid, HashSet<Guid>> mapObjectGuidsByEarnItem)
        {
            // 毎フレームの候補判定がO(1)になるよう集合で持つ（IReadOnlySetはUnityのAPI互換レベルで使えない）
            // Keep sets so the per-frame candidate test is O(1); IReadOnlySet is unavailable at Unity's API compatibility level
            mapObjectGuidsByEarnItem = new Dictionary<Guid, HashSet<Guid>>();
            foreach (var mapObjectElement in map.MapObjects)
            {
                foreach (var earnItem in mapObjectElement.EarnItems)
                {
                    if (!mapObjectGuidsByEarnItem.TryGetValue(earnItem.ItemGuid, out var mapObjectGuids))
                    {
                        mapObjectGuids = new HashSet<Guid>();
                        mapObjectGuidsByEarnItem.Add(earnItem.ItemGuid, mapObjectGuids);
                    }

                    mapObjectGuids.Add(mapObjectElement.MapObjectGuid);
                }
            }
        }
    }
}
