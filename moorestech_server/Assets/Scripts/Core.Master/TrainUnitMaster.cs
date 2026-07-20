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

        private Dictionary<Guid, TrainCarMasterElement> _trainCarMastersByGuid;

        public double Friction => Train.MotionParameters.Friction / (double)10000000;
        public double AirResistance => Train.MotionParameters.AirResistance / (double)10000000;
        public double MaxBrakeDecelerationMetersPerSecondSquared => Train.MotionParameters.MaxBrakeDecelerationKmhPerSecond / 3.6d;
        public int MasconLevelMaximum => Train.MotionParameters.MasconLevelMaximum;

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
            TrainUnitMasterUtil.Initialize(Train, out _trainCarMastersByGuid);
        }

        public bool TryGetTrainCarMaster(Guid guid, out TrainCarMasterElement element)
        {
            return _trainCarMastersByGuid.TryGetValue(guid, out element);
        }
    }
}

