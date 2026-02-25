using System.Collections.Generic;
using Game.Train.Unit;

namespace Game.Train.Diagram
{
    public interface ITrainDiagramCar
    {
        bool IsInventoryFull();
        bool IsInventoryEmpty();
    }

    public interface ITrainDiagramContext
    {
        TrainInstanceId TrainInstanceId { get; }
        IReadOnlyList<ITrainDiagramCar> Cars { get; }
        bool IsAutoRun { get; }
        bool IsDocked { get; }
        void OnCurrentEntryShiftedByRemoval();
    }
}
