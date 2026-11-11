using Game.Train.Unit;

namespace Client.Ui.State.Train.TimetableEdit
{
    /// <summary>
    /// 時刻表タブの中身。読込中・使えない・列車ごとの区画の 3 つ
    /// Content of the timetable tab: loading, unavailable, or a per-train section
    /// </summary>
    public abstract class TrainTimetableAreaViewModel
    {
        public static readonly TrainTimetableAreaViewModel Loading = new LoadingTrainTimetableArea();
        public static readonly TrainTimetableAreaViewModel Unavailable = new UnavailableTrainTimetableArea();

        public static TrainTimetableAreaViewModel Ready(TrainUnitInstanceId trainUnitInstanceId, TrainTimetableSectionViewModel section)
        {
            return new ReadyTrainTimetableArea(trainUnitInstanceId, section);
        }

        public static bool IsSameArea(TrainTimetableAreaViewModel current, TrainTimetableAreaViewModel next)
        {
            if (current is ReadyTrainTimetableArea currentReady && next is ReadyTrainTimetableArea nextReady)
            {
                return currentReady.IsSameTrain(nextReady) && currentReady.Section.HasSameContent(nextReady.Section);
            }
            return ReferenceEquals(current, next);
        }
    }

    public sealed class LoadingTrainTimetableArea : TrainTimetableAreaViewModel
    {
        internal LoadingTrainTimetableArea() { }
    }

    public sealed class UnavailableTrainTimetableArea : TrainTimetableAreaViewModel
    {
        internal UnavailableTrainTimetableArea() { }
    }

    public sealed class ReadyTrainTimetableArea : TrainTimetableAreaViewModel
    {
        private readonly TrainUnitInstanceId _trainUnitInstanceId;
        public TrainTimetableSectionViewModel Section { get; }

        internal ReadyTrainTimetableArea(TrainUnitInstanceId trainUnitInstanceId, TrainTimetableSectionViewModel section)
        {
            _trainUnitInstanceId = trainUnitInstanceId;
            Section = section;
        }

        // 同じ列車か。View は列車の型（Game.Block.Interface）を参照できないのでこれで区画の作り直しを決める
        // Whether it is the same train; the View cannot reference the train type (Game.Block.Interface), so it decides section rebuilds with this
        public bool IsSameTrain(ReadyTrainTimetableArea other)
        {
            return _trainUnitInstanceId.Equals(other._trainUnitInstanceId);
        }
    }
}
