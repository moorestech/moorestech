using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Loader.TrainModule;
using Mooresmaster.Model.TrainModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class TrainUnitMaster : IMasterValidator
    {
        public readonly Train Train;

        private Dictionary<ItemId, TrainCarMasterElement> _trainCarMastersByItemId;
        private Dictionary<Guid, TrainCarMasterElement> _trainCarMastersByGuid;

        public TrainUnitMaster(JToken jToken)
        {
            Train = TrainLoader.Load(jToken);
        }

        public bool Validate(out string errorLogs)
        {
            errorLogs = "";
            errorLogs += TrainCarValidation();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string TrainCarValidation()
            {
                var logs = "";
                foreach (var trainCar in Train.TrainCars)
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

        public void Initialize()
        {
            // Dictionary構築（Validate成功後に実行）
            // Build dictionaries (executed after Validate succeeds)
            _trainCarMastersByItemId = Train.TrainCars.ToDictionary(car => MasterHolder.ItemMaster.GetItemId(car.ItemGuid), car => car);
            _trainCarMastersByGuid = Train.TrainCars.ToDictionary(car => car.TrainCarGuid, car => car);
        }

        public bool TryGetTrainUnit(ItemId itemId, out TrainCarMasterElement element)
        {
            return _trainCarMastersByItemId.TryGetValue(itemId, out element);
        }

        public bool TryGetTrainUnit(Guid guid, out TrainCarMasterElement element)
        {
            return _trainCarMastersByGuid.TryGetValue(guid, out element);
        }
    }
}

