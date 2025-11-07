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
        
        private readonly Dictionary<ItemId, TrainCarMasterElement> _trainCraMastersByItemId;
        private readonly Dictionary<Guid, TrainCarMasterElement> _trainCarMastersByGuid;

        public TrainUnitMaster(JToken jToken, ItemMaster itemMaster)
        {
            Train = TrainLoader.Load(jToken);
            _trainCraMastersByItemId = Train.TrainCars.ToDictionary(car => MasterHolder.ItemMaster.GetItemId(car.ItemGuid), car => car);
            _trainCarMastersByGuid = Train.TrainCars.ToDictionary(car => car.TrainCarGuid, car => car);
        }

        public bool TryGetTrainUnit(ItemId itemId, out TrainCarMasterElement element)
        {
            return _trainCraMastersByItemId.TryGetValue(itemId, out element);
        }
        
        public bool TryGetTrainUnit(Guid guid, out TrainCarMasterElement element)
        {
            return _trainCarMastersByGuid.TryGetValue(guid, out element);
        }
    }
}

