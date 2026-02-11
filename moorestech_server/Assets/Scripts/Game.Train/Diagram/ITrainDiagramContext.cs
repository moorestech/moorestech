using System;
using System.Collections.Generic;

namespace Game.Train.Diagram
{
    public interface ITrainDiagramCar
    {
        bool IsInventoryFull();
        bool IsInventoryEmpty();
    }

    public interface ITrainDiagramContext
    {
        Guid TrainId { get; }
        IReadOnlyList<ITrainDiagramCar> Cars { get; }
        bool IsAutoRun { get; }
        bool IsDocked { get; }
        void OnCurrentEntryShiftedByRemoval();
    }
}
