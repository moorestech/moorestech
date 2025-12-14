using Mooresmaster.Model.PlaceSystemModule;

namespace Core.Master.Validator
{
    public static class PlaceSystemMasterUtil
    {
        public static bool Validate(PlaceSystem placeSystem, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += PlaceItemValidation();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string PlaceItemValidation()
            {
                var logs = "";
                for (var i = 0; i < placeSystem.Data.Length; i++)
                {
                    var element = placeSystem.Data[i];
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

        public static void Initialize(PlaceSystem placeSystem)
        {
            // PlaceSystemMasterは追加の初期化処理がないため、空実装
            // PlaceSystemMaster has no additional initialization, so empty implementation
        }
    }
}
