using Mooresmaster.Model.MapModule;

namespace Core.Master.Validator
{
    public static class MapObjectMasterUtil
    {
        public static bool Validate(Map map, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += ItemGuidValidation();
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
                                continue;
                            }

                            // itemsのtoolsに無いツールは装備できず、そのmapObjectが恒久的に採掘不能になる
                            // A tool missing from items' tools can never be equipped, making the mapObject permanently unmineable
                            if (!MasterHolder.ToolMaster.IsTool(itemId.Value))
                            {
                                logs += $"[MapObjectMaster] Name:{mapObjectElement.MapObjectName} has ToolItemGuid:{miningTool.ToolItemGuid} that is not registered in items tools\n";
                            }
                        }
                    }
                }

                return logs;
            }

            #endregion
        }

        public static void Initialize(Map map)
        {
            // MapObjectMasterは追加の初期化処理がないため、空実装
            // MapObjectMaster has no additional initialization, so empty implementation
        }
    }
}
