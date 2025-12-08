using System;
using Mooresmaster.Loader.MapObjectsModule;
using Mooresmaster.Model.MapObjectsModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class MapObjectMaster : IMasterValidator
    {
        public readonly MapObjects MapObjects;

        public MapObjectMaster(JToken jToken)
        {
            MapObjects = MapObjectsLoader.Load(jToken);
        }

        public bool Validate(out string errorLogs)
        {
            errorLogs = "";
            errorLogs += ItemGuidValidation();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string ItemGuidValidation()
            {
                var logs = "";
                foreach (var mapObjectElement in MapObjects.Data)
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

        public void Initialize()
        {
            // MapObjectMasterは追加の初期化処理がないため、空実装
            // MapObjectMaster has no additional initialization, so empty implementation
        }

        public MapObjectMasterElement GetMapObjectElement(Guid guid)
        {
            return Array.Find(MapObjects.Data, x => x.MapObjectGuid == guid);
        }
    }
}