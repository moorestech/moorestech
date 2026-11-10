using System;
using Client.Localization;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Train.Inventory;
using Client.Ui.State.Train.Timetable;
using Client.Ui.State.Train.TimetableEdit;
using Client.Ui.State.Train.TimetableEdit.Send;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// 列車の本文の Presenter。タブの局所状態と取得の契機を持ち、中身・時刻表の Model を購読して View へ流す
    /// Presenter of the train body: owns the local tab state and fetch trigger, and subscribes the contents and timetable models into the View
    /// </summary>
    public sealed class TrainInventoryBodyPresenter : ITrainInventoryPart
    {
        private readonly TrainInventoryBodyView _view;
        private readonly ITrainInventoryBodySource _body;
        private readonly ITrainTimetableEditModel _timetable;
        private readonly ITrainTimetableSource _timetableSource;
        private readonly ITrainTimetableCommands _timetableCommands;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly SerialDisposable _shownTabSubscription = new();
        private TrainTimetableFetchTrigger _trigger = new();
        private BlockInventoryPanelState _panel = BlockInventoryPanelState.Closed;

        public VisualElement Root => _view;

        public TrainInventoryBodyPresenter(TrainInventoryBodyView view, ITrainInventoryBodySource body, ITrainTimetableEditModel timetable, ITrainTimetableSource timetableSource, ITrainTimetableCommands timetableCommands)
        {
            _view = view;
            _body = body;
            _timetable = timetable;
            _timetableSource = timetableSource;
            _timetableCommands = timetableCommands;
        }

        public void Attach(OpenTrainInventoryPanel panel)
        {
            // 別のパネルでは持ち物タブから（Web は本文の mount ごとに初期化）。隠して戻した同じ車両はタブを保つ
            // A different panel starts on the inventory tab (Web resets per body mount); the same car shown again keeps its tab
            if (!panel.IsSamePanel(_panel)) _trigger = new TrainTimetableFetchTrigger();
            _panel = panel;
            _view.OnTabSelected.Subscribe(SelectTab).AddTo(_subscriptions);
            _timetableSource.Current.Subscribe(state =>
            {
                if (_trigger.ReceiveState(state)) _timetableCommands.RequestOpen();
            }).AddTo(_subscriptions);
            RenderTab(_trigger.Tab);
        }

        public void Detach()
        {
            _subscriptions.Clear();
            _shownTabSubscription.Disposable = null;
        }

        // 取得中の表示は Fetcher の開始の合図で源が組み直して出す（D-20）。重複の依頼は Fetcher が畳む
        // Loading shows when the source rebuilds on the Fetcher's start notification (D-20); the Fetcher folds duplicates
        private void SelectTab(TrainInventoryTab tab)
        {
            var enteredTimetable = _trigger.SelectTab(tab);
            RenderTab(tab);
            if (enteredTimetable) _timetableCommands.RequestOpen();
        }

        // 表示中のタブの Model だけを購読する。非表示の間に描かないのは Presenter の購読で守る（設計書 §4.2 の SetShown の注記。購読の頭で最新値を 1 回描く）
        // Subscribe only the shown tab's model; the Presenter's subscription keeps hidden tabs from drawing (design doc §4.2 SetShown note; the latest value draws once on subscribe)
        private void RenderTab(TrainInventoryTab tab)
        {
            _view.RenderTab(tab);
            _shownTabSubscription.Disposable = tab == TrainInventoryTab.Timetable ? SubscribeTimetable() : SubscribeContents();
        }

        private IDisposable SubscribeContents()
        {
            return _body.Current.Subscribe(_view.Contents.Render);
        }

        private IDisposable SubscribeTimetable()
        {
            var subscriptions = new CompositeDisposable();
            _timetable.Current.Subscribe(_ => RenderTimetable()).AddTo(subscriptions);
            Localize.OnLanguageChanged.Subscribe(_ => RenderTimetable()).AddTo(subscriptions);
            return subscriptions;
        }

        // 無名の駅の名前は言語で変わるので、言語の変化でも描き直す（D-10）
        // The unnamed station name localizes, so language changes redraw too (D-10)
        private void RenderTimetable()
        {
            _view.Timetable.Render(_timetable.Current.Value, Localize.Get(LocalizationKeys.Ui.BlockInventory.TimetableUnnamedStation));
        }
    }
}
