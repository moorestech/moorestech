using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Loader.TrainModule;
using Mooresmaster.Model.TrainModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class TrainUnitMaster
    {
        public readonly Train Train;
        
        private readonly Dictionary<ItemId, TrainCarMasterElement> _trainCarMastersByItemId;
        private readonly Dictionary<Guid, TrainCarMasterElement> _trainCarMastersByGuid;

        public TrainUnitMaster(JToken jToken, ItemMaster itemMaster)
        {
            Train = TrainLoader.Load(jToken);

            // 外部キーバリデーション（Dictionary構築前に実行）
            // Foreign key validation (executed before Dictionary construction)
            TrainCarValidation();

            _trainCarMastersByItemId = Train.TrainCars.ToDictionary(car => MasterHolder.ItemMaster.GetItemId(car.ItemGuid), car => car);
            _trainCarMastersByGuid = Train.TrainCars.ToDictionary(car => car.TrainCarGuid, car => car);

            #region Internal

            void TrainCarValidation()
            {
                var errorLogs = "";
                foreach (var trainCar in Train.TrainCars)
                {
                    var itemId = itemMaster.GetItemIdOrNull(trainCar.ItemGuid);
                    if (itemId == null)
                    {
                        errorLogs += $"[TrainUnitMaster] TrainCar:{trainCar.TrainCarGuid} has invalid ItemGuid:{trainCar.ItemGuid}\n";
                    }
                }

                if (!string.IsNullOrEmpty(errorLogs))
                {
                    throw new Exception(errorLogs);
                }
            }

            #endregion
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

