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
        TrainUnitInstanceId TrainUnitInstanceId { get; }
        IReadOnlyList<ITrainDiagramCar> Cars { get; }
        bool IsAutoRun { get; }
        bool IsDocked { get; }
        void OnCurrentEntryShiftedByRemoval();

        // 時刻表の内容や現在地が実際に変わった時点で呼ぶ
        // Called at the moment the timetable contents or cursor actually changed
        void OnTimetableChanged();
    }
}
