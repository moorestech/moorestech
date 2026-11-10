using System;
using Client.Ui.State.Toast;
using Client.Ui.State.Train.Timetable;
using Client.Ui.State.Train.TimetableEdit.Send;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Train.TimetableEdit
{
    /// <summary>
    /// 列車ごとの編集セッションの器。描画値の Model が読み、編集のコマンドが書き換える（前例 PauseMenuStateService）
    /// Holder of the per-train edit session; the draw-value model reads it and the edit commands mutate it (precedent PauseMenuStateService)
    /// </summary>
    public sealed class TrainTimetableEditState : IInitializable
    {
        private readonly ITrainTimetableSource _source;
        private readonly ITrainTimetableCommands _commands;
        private readonly ITrainTimetableWaitTimer _timer;
        private readonly UiCommandResultToaster _toaster;
        private readonly Subject<Unit> _onChanged = new();
        private readonly SerialDisposable _sessionSubscription = new();
        private TrainTimetableEditSession _session;

        // セッションが無いあいだ読込中か（偽なら使えない）。源の最新の状態から決める
        // Whether it is loading while no session exists (false means unavailable), from the source's latest state
        public bool IsLoading { get; private set; }

        // セッションの開閉・表示の変化・源の状態の変化のたびに流す
        // Fires on session open/close, display changes and source state changes
        public IObservable<Unit> OnChanged => _onChanged;

        public TrainTimetableEditState(ITrainTimetableSource source, ITrainTimetableCommands commands, ITrainTimetableWaitTimer timer, UiCommandResultToaster toaster)
        {
            _source = source;
            _commands = commands;
            _timer = timer;
            _toaster = toaster;
        }

        public void Initialize()
        {
            _source.Current.Subscribe(Receive);
        }

        // 開いている列車のセッション。時刻表が開いていなければ偽
        // The open train's session; false when no timetable is open
        public bool TryGetSession(out TrainTimetableEditSession session)
        {
            session = _session;
            return _session != null;
        }

        private void Receive(TrainTimetableViewState state)
        {
            if (state is ReadyTrainTimetableViewState ready)
            {
                ReceiveReady(ready.Data);
                return;
            }
            CloseSession();
            IsLoading = state is LoadingTrainTimetableViewState;
            _onChanged.OnNext(Unit.Default);
        }

        private void ReceiveReady(TrainTimetableReadyData data)
        {
            // 列車が替わったら編集を作り直し（Web の key=trainUnitId）、同じ列車ならサーバーの並びを編集へ渡す（表示の変化はセッションの通知で流れる）
            // A different train rebuilds the session (Web key=trainUnitId); the same train hands the server order to the session (its notification carries the change)
            if (_session == null || !_session.Server.TrainUnitInstanceId.Equals(data.TrainUnitInstanceId))
            {
                OpenSession(data);
                return;
            }
            _session.ReceiveServer(data);
        }

        private void OpenSession(TrainTimetableReadyData data)
        {
            CloseSession();
            _session = new TrainTimetableEditSession(_commands, _timer, _toaster, data);
            _sessionSubscription.Disposable = _session.OnDisplayChanged.Subscribe(_ => _onChanged.OnNext(Unit.Default));
            _onChanged.OnNext(Unit.Default);
        }

        private void CloseSession()
        {
            _sessionSubscription.Disposable = null;
            _session?.Close();
            _session = null;
        }
    }
}
