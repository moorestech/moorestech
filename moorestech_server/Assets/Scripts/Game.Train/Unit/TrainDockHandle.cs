using System;
using Game.Block.Interface.Component;

namespace Game.Train.Unit
{
    public sealed class TrainDockHandle : ITrainDockHandle
    {
        private readonly TrainUnit _trainUnit;
        private readonly TrainCar _trainCar;
        private readonly int _carIndex;

        public TrainDockHandle(TrainUnit trainUnit, TrainCar trainCar, int carIndex)
        {
            _trainUnit = trainUnit;
            _trainCar = trainCar;
            _carIndex = carIndex;
        }

        public Guid TrainId => _trainUnit.TrainId;
        public long TrainCarInstanceId => _trainCar.TrainCarInstanceId.AsPrimitive();
        public int CarIndex => _carIndex;

        public TrainUnit TrainUnit => _trainUnit;
        public TrainCar TrainCar => _trainCar;
    }
}
