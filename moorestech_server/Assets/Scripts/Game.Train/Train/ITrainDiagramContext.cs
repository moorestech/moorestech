using System;
using System.Collections.Generic;

namespace Game.Train.Train
{
    public interface ITrainDiagramContext
    {
        Guid TrainId { get; }
        IReadOnlyList<TrainCar> Cars { get; }
        bool IsAutoRun { get; }
        bool IsDocked { get; }
    }
}
