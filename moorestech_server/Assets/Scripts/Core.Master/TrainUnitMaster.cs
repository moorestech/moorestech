using System;
using System.Collections.Generic;
using Core.Master.Validator;
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
            return TrainUnitMasterUtil.Validate(Train, out errorLogs);
        }

        public void Initialize()
        {
            TrainUnitMasterUtil.Initialize(Train, out _trainCarMastersByItemId, out _trainCarMastersByGuid);
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

