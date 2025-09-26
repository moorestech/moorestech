using System;
using Game.Block.Interface.Component;
using Game.Train.Train;

namespace Game.Train.Common
{
    internal sealed class TrainDockHandle : ITrainDockHandle
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
        public Guid CarId => _trainCar.CarId;
        public int CarIndex => _carIndex;

        internal TrainUnit TrainUnit => _trainUnit;
        internal TrainCar TrainCar => _trainCar;
    }
}