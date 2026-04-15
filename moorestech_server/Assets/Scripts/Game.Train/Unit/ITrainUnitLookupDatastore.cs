using System.Collections.Generic;

namespace Game.Train.Unit
{
    public interface ITrainUnitLookupDatastore
    {
        IReadOnlyCollection<TrainUnit> GetRegisteredTrains();
        bool TryGetTrainUnit(TrainInstanceId id, out TrainUnit trainUnit);
        bool TryGetTrainUnitByCar(TrainCarInstanceId id, out TrainUnit trainUnit);
    }
}
