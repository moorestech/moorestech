using System;
using Game.Block.Interface.Component;
using Game.Train.Train;

namespace Game.Train.Common
{
    internal sealed class TrainDockHandle : ITrainDockHandle
    {
        private readonly TrainUnit _trainUnit;

        public TrainDockHandle(TrainUnit trainUnit)
        {
            _trainUnit = trainUnit;
        }

        public Guid TrainId => _trainUnit.TrainId;

        internal TrainUnit TrainUnit => _trainUnit;
    }
}