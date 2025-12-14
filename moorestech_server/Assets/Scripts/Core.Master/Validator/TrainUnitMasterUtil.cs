using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Model.TrainModule;

namespace Core.Master.Validator
{
    public static class TrainUnitMasterUtil
    {
        public static bool Validate(Train train, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += TrainCarValidation();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string TrainCarValidation()
            {
                var logs = "";
                foreach (var trainCar in train.TrainCars)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(trainCar.ItemGuid);
                    if (itemId == null)
                    {
                        logs += $"[TrainUnitMaster] TrainCar:{trainCar.TrainCarGuid} has invalid ItemGuid:{trainCar.ItemGuid}\n";
                    }
                }

                return logs;
            }

            #endregion
        }

        public static void Initialize(
            Train train,
            out Dictionary<ItemId, TrainCarMasterElement> trainCarMastersByItemId,
            out Dictionary<Guid, TrainCarMasterElement> trainCarMastersByGuid)
        {
            // Dictionary構築（Validate成功後に実行）
            // Build dictionaries (executed after Validate succeeds)
            trainCarMastersByItemId = train.TrainCars.ToDictionary(car => MasterHolder.ItemMaster.GetItemId(car.ItemGuid), car => car);
            trainCarMastersByGuid = train.TrainCars.ToDictionary(car => car.TrainCarGuid, car => car);
        }
    }
}
