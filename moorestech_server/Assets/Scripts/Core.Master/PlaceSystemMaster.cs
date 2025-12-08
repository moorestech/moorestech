using System;
using Mooresmaster.Loader.PlaceSystemModule;
using Mooresmaster.Model.PlaceSystemModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class PlaceSystemMaster
    {
        public readonly PlaceSystem PlaceSystem;

        public PlaceSystemMaster(JToken jToken)
        {
            PlaceSystem = PlaceSystemLoader.Load(jToken);

            // 外部キーバリデーション
            // Foreign key validation
            PlaceItemValidation();

            #region Internal

            void PlaceItemValidation()
            {
                var errorLogs = "";
                for (var i = 0; i < PlaceSystem.Data.Length; i++)
                {
                    var element = PlaceSystem.Data[i];
                    foreach (var itemGuid in element.UsePlaceItems)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(itemGuid);
                        if (itemId == null)
                        {
                            errorLogs += $"[PlaceSystemMaster] PlaceMode:{element.PlaceMode} has invalid UsePlaceItem:{itemGuid}\n";
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
    }
}