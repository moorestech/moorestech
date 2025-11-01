using System;
using System.Collections.Generic;
using Mooresmaster.Loader.TrainModule;
using Mooresmaster.Model.TrainModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class TrainUnitMaster
    {
        public readonly Train Train;
        private readonly Dictionary<ItemId, TrainUnitMasterElement> _unitsByItemId;

        public TrainUnitMaster(JToken jToken, ItemMaster itemMaster)
        {
            Train = TrainLoader.Load(jToken);
            _unitsByItemId = new Dictionary<ItemId, TrainUnitMasterElement>();
        }

        public bool TryGetTrainUnit(ItemId itemId, out TrainUnitMasterElement element)
        {
            return _unitsByItemId.TryGetValue(itemId, out element);
        }
    }
}

