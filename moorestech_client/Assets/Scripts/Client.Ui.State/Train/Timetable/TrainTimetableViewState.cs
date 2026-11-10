namespace Client.Ui.State.Train.Timetable
{
    /// <summary>
    /// 時刻表タブに出す状態。読込中・使えない・用意済みの 3 つ
    /// State shown in the timetable tab: loading, unavailable, or ready
    /// </summary>
    public abstract class TrainTimetableViewState
    {
        public static readonly TrainTimetableViewState Loading = new LoadingTrainTimetableViewState();
        public static readonly TrainTimetableViewState Unavailable = new UnavailableTrainTimetableViewState();

        public static TrainTimetableViewState Ready(TrainTimetableReadyData data)
        {
            return new ReadyTrainTimetableViewState(data);
        }

        // 同じ状態は流し直さない。用意済みは中身で比べる
        // Same states are not republished; ready compares contents
        public static bool IsSameState(TrainTimetableViewState current, TrainTimetableViewState next)
        {
            if (current is ReadyTrainTimetableViewState currentReady && next is ReadyTrainTimetableViewState nextReady) return currentReady.Data.HasSameContent(nextReady.Data);
            return ReferenceEquals(current, next);
        }
    }

    public sealed class LoadingTrainTimetableViewState : TrainTimetableViewState
    {
        internal LoadingTrainTimetableViewState() { }
    }

    public sealed class UnavailableTrainTimetableViewState : TrainTimetableViewState
    {
        internal UnavailableTrainTimetableViewState() { }
    }

    public sealed class ReadyTrainTimetableViewState : TrainTimetableViewState
    {
        public TrainTimetableReadyData Data { get; }

        internal ReadyTrainTimetableViewState(TrainTimetableReadyData data)
        {
            Data = data;
        }
    }
}
