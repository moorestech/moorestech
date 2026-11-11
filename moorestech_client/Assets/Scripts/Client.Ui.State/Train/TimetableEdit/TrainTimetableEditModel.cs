using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Train.TimetableEdit
{
    /// <summary>
    /// 編集の器を購読し、時刻表タブの描画値（読込中・使えない・列車ごとの区画）を配る
    /// Subscribes to the edit holder and publishes the timetable tab's draw values (loading, unavailable or a per-train section)
    /// </summary>
    public sealed class TrainTimetableEditModel : IInitializable
    {
        private readonly TrainTimetableEditState _state;
        private readonly ReactiveProperty<TrainTimetableAreaViewModel> _current = new(TrainTimetableAreaViewModel.Unavailable);

        public IReadOnlyReactiveProperty<TrainTimetableAreaViewModel> Current => _current;

        public TrainTimetableEditModel(TrainTimetableEditState state)
        {
            _state = state;
        }

        public void Initialize()
        {
            // 器の Initialize が先でも後でも、購読の後に 1 回組めば最新を配れる
            // Whether the holder initializes first or later, building once after subscribing publishes the latest
            _state.OnTimetableAreaInputChanged.Subscribe(_ => Rebuild());
            Rebuild();
        }

        private void Rebuild()
        {
            var next = Build();
            // silent-ok: 同じ中身は配り直さない / 表示が変わらないので見ている人に差は無い（R-5）
            if (TrainTimetableAreaViewModel.IsSameArea(_current.Value, next)) return;
            _current.Value = next;
        }

        private TrainTimetableAreaViewModel Build()
        {
            if (!_state.TryGetSession(out var session)) return _state.IsLoading ? TrainTimetableAreaViewModel.Loading : TrainTimetableAreaViewModel.Unavailable;
            var section = TrainTimetableSectionViewModel.Build(session.Server, session.DisplayedStops, session.IsPending);
            return TrainTimetableAreaViewModel.Ready(session.Server.TrainUnitInstanceId, section);
        }
    }
}
