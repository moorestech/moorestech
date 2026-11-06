using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Ui.State.Routing;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Train
{
    /// <summary>
    /// 画面ルータ・UIState・列車 HUD の表示変化で乗車 HUD を作り直す。乗車の変化は状態の持ち主 TrainHUDScreenState の通知だけで受ける
    /// Rebuilds the riding HUD on router, UIState and train HUD presentation changes; riding changes arrive only through the owner TrainHUDScreenState
    /// </summary>
    public sealed class TrainRidingHudModel : IInitializable
    {
        private readonly UiRoutingState _routingState;
        private readonly UIStateControl _uiStateControl;
        private readonly TrainHUDScreenState _trainHudState;
        private readonly ReactiveProperty<TrainRidingHudViewModel> _current;

        public IReadOnlyReactiveProperty<TrainRidingHudViewModel> Current => _current;

        public TrainRidingHudModel(UiRoutingState routingState, UIStateControl uiStateControl, TrainHUDScreenState trainHudState)
        {
            _routingState = routingState;
            _uiStateControl = uiStateControl;
            _trainHudState = trainHudState;
            _current = new ReactiveProperty<TrainRidingHudViewModel>(Build());
        }

        public void Initialize()
        {
            // 乗車の変化は OnPresentationChanged が知らせる（生のパケットは購読しない。D-20）。ゲーム寿命なので購読は解かない
            // OnPresentationChanged reports riding changes (no raw packet subscription; D-20); it lives for the whole game, so subscriptions stay
            _routingState.Current.Skip(1).Subscribe(_ => Rebuild());
            _uiStateControl.OnStateChanged.Subscribe(_ => Rebuild());
            _trainHudState.OnPresentationChanged.Subscribe(_ => Rebuild());
            Rebuild();
        }

        private void Rebuild()
        {
            _current.SetValueAndForceNotify(Build());
        }

        private TrainRidingHudViewModel Build()
        {
            var screen = _routingState.Current.Value.Screen;
            return TrainRidingHudViewModel.Create(screen, _uiStateControl.CurrentState, _trainHudState.IsRiding, _trainHudState.BranchCandidateCount, _trainHudState.SelectedBranchIndex);
        }
    }
}
