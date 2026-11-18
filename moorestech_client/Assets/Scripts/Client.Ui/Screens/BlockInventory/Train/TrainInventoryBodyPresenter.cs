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
    /// 列車本文の Presenter。タブ状態と取得契機を持ち Model を View へ流す
    /// Train body Presenter: owns tab state and fetch trigger, forwards Models to View
    /// </summary>
    public sealed class TrainInventoryBodyPresenter : ITrainInventoryPart
    {
        private readonly TrainInventoryBodyView _view;
        private readonly ITrainInventoryBodySource _body;
        private readonly TrainTimetableEditModel _timetable;
        private readonly ITrainTimetableSource _timetableSource;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly SerialDisposable _shownTabSubscription = new();
        private readonly TrainTimetableFetchTrigger _trigger;

        public VisualElement Root => _view;

        public TrainInventoryBodyPresenter(TrainInventoryBodyView view, ITrainInventoryBodySource body, TrainTimetableEditModel timetable, ITrainTimetableSource timetableSource, ITrainTimetableCommands timetableCommands)
        {
            _view = view;
            _body = body;
            _timetable = timetable;
            _timetableSource = timetableSource;
            _trigger = new TrainTimetableFetchTrigger(timetableCommands);
        }

        public void Attach(OpenTrainInventoryPanel panel)
        {
            // 本文は付け替えのたびに作り直されるので、局所状態は持ち物タブから始まる（Web は本文の mount ごとに初期化）
            // The body is rebuilt on every swap, so local state starts on the inventory tab (Web resets per body mount)
            _view.OnTabSelected.Subscribe(SelectTab).AddTo(_subscriptions);
            _timetableSource.Current.Subscribe(_trigger.ReceiveState).AddTo(_subscriptions);
            RenderTab(_trigger.Tab);

            #region Internal

            // 取得中の表示は Fetcher の開始の合図で源が組み直して出す（D-20）。重複の依頼は Fetcher が畳む
            // Loading shows when the source rebuilds on the Fetcher's start notification (D-20); the Fetcher folds duplicates
            void SelectTab(TrainInventoryTab tab)
            {
                _trigger.SelectTab(tab);
                RenderTab(tab);
            }

            #endregion
        }

        public void Detach()
        {
            _subscriptions.Clear();
            _shownTabSubscription.Disposable = null;
        }

        // 表示中のタブの Model だけを購読する。非表示の間に描かないのは Presenter の購読で守る（設計書 §4.2 の SetShown の注記。購読の頭で最新値を 1 回描く）
        // Subscribe only the shown tab's model; the Presenter's subscription keeps hidden tabs from drawing (design doc §4.2 SetShown note; the latest value draws once on subscribe)
        private void RenderTab(TrainInventoryTab tab)
        {
            _view.RenderTab(tab);
            _shownTabSubscription.Disposable = tab == TrainInventoryTab.Timetable ? SubscribeTimetable() : SubscribeContents();

            #region Internal

            IDisposable SubscribeContents()
            {
                return _body.Current.Subscribe(_view.Contents.Render);
            }

            IDisposable SubscribeTimetable()
            {
                var subscriptions = new CompositeDisposable();
                _timetable.Current.Subscribe(_ => RenderTimetable()).AddTo(subscriptions);
                Localize.OnLanguageChanged.Subscribe(_ => RenderTimetable()).AddTo(subscriptions);
                return subscriptions;
            }

            // 無名の駅の名前は言語で変わるので、言語の変化でも描き直す（D-10）
            // The unnamed station name localizes, so language changes redraw too (D-10)
            void RenderTimetable()
            {
                _view.Timetable.Render(_timetable.Current.Value, Localize.Get(LocalizationKeys.Ui.BlockInventory.TimetableUnnamedStation));
            }

            #endregion
        }
    }
}
