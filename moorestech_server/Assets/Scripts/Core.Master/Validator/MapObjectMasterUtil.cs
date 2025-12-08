using Mooresmaster.Model.MapObjectsModule;

namespace Core.Master.Validator
{
    public static class MapObjectMasterUtil
    {
        public static bool Validate(MapObjects mapObjects, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += ItemGuidValidation();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string ItemGuidValidation()
            {
                var logs = "";
                foreach (var mapObjectElement in mapObjects.Data)
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

            #endregion
        }

        public static void Initialize(MapObjects mapObjects)
        {
            // MapObjectMasterは追加の初期化処理がないため、空実装
            // MapObjectMaster has no additional initialization, so empty implementation
        }
    }
}
