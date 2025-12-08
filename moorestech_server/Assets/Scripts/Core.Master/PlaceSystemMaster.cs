using System;
using Mooresmaster.Loader.PlaceSystemModule;
using Mooresmaster.Model.PlaceSystemModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class PlaceSystemMaster : IMasterValidator
    {
        public readonly PlaceSystem PlaceSystem;

        public PlaceSystemMaster(JToken jToken)
        {
            PlaceSystem = PlaceSystemLoader.Load(jToken);
        }

        public bool Validate(out string errorLogs)
        {
            errorLogs = "";
            errorLogs += PlaceItemValidation();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string PlaceItemValidation()
            {
                var logs = "";
                for (var i = 0; i < PlaceSystem.Data.Length; i++)
                {
                    var element = PlaceSystem.Data[i];
                    foreach (var itemGuid in element.UsePlaceItems)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(itemGuid);
                        if (itemId == null)
                        {
                            logs += $"[PlaceSystemMaster] PlaceMode:{element.PlaceMode} has invalid UsePlaceItem:{itemGuid}\n";
                        }
                    }
                }

                return logs;
            }

            #endregion
        }

        public void Initialize()
        {
            // PlaceSystemMasterは追加の初期化処理がないため、空実装
            // PlaceSystemMaster has no additional initialization, so empty implementation
        }
    }
}