using System;
using Mooresmaster.Loader.MapObjectsModule;
using Mooresmaster.Model.MapObjectsModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class MapObjectMaster
    {
        public readonly MapObjects MapObjects;
        
        public MapObjectMaster(JToken jToken)
        {
            MapObjects = MapObjectsLoader.Load(jToken);
            
            ItemGuidValidation();
            
            #region Internal
            
            void ItemGuidValidation()
            {
                var errorLogs = "";
                foreach (var mapObjectElement in MapObjects.Data)
                {
                    foreach (var earnItemsElement in mapObjectElement.EarnItems)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(earnItemsElement.ItemGuid);
                        if (itemId == null)
                        {
                            errorLogs += $"[MapObjectMaster] Name:{mapObjectElement.MapObjectName} has invalid ItemGuid:{earnItemsElement.ItemGuid}\n";
                        }
                    }
                    
                    var miningParam = mapObjectElement.MiningParam;
                    if (miningParam is MiningMiningParam)
                    {
                        foreach (var miningTool in ((MiningMiningParam)miningParam).MiningTools)
                        {
                            var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(miningTool.ToolItemGuid);
                            if (itemId == null)
                            {
                                errorLogs += $"[MapObjectMaster] Name:{mapObjectElement.MapObjectName} has invalid ToolItemGuid:{miningTool.ToolItemGuid}\n";
                            }
                        }
                    }
                }
                
                if (!string.IsNullOrEmpty(errorLogs))
                {
                    throw new Exception(errorLogs);
                }
            }
            
            #endregion
        }
        
        public MapObjectMasterElement GetMapObjectElement(Guid guid)
        {
            return Array.Find(MapObjects.Data, x => x.MapObjectGuid == guid);
        }
    }
}