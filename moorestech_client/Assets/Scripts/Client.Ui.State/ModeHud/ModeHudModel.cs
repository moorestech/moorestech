using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.ModeHud
{
    /// <summary>
    /// UI 状態・設置対象・高さ・言語を購読し、操作モード HUD の表示状態を配る
    /// Subscribes to the UI state, placement target, height and language and publishes the operation-mode HUD state
    /// </summary>
    public sealed class ModeHudModel : IInitializable
    {
        private readonly UIStateControl _uiStateControl;
        private readonly PlaceSystemStateController _placeSystemStateController;
        private readonly PlacementHeightOffset _placementHeightOffset;
        private readonly ReactiveProperty<ModeHudViewModel> _current;

        public IReadOnlyReactiveProperty<ModeHudViewModel> Current => _current;

        public ModeHudModel(UIStateControl uiStateControl, PlaceSystemStateController placeSystemStateController, PlacementHeightOffset placementHeightOffset)
        {
            _uiStateControl = uiStateControl;
            _placeSystemStateController = placeSystemStateController;
            _placementHeightOffset = placementHeightOffset;
            _current = new ReactiveProperty<ModeHudViewModel>(Build());
        }

        public void Initialize()
        {
            // 変化の源 4 本。表示名は言語で変わるので言語の変化でも組み直す
            // Four change sources; the display name depends on the language, so rebuild on language changes too
            _uiStateControl.OnStateChanged.Subscribe(_ => Rebuild());
            _placeSystemStateController.OnTargetChanged.Subscribe(_ => Rebuild());
            _placementHeightOffset.OnChanged.Subscribe(_ => Rebuild());
            Localize.OnLanguageChanged.Subscribe(_ => Rebuild());

            // ctor から購読までの間の変化を取り込む
            // Pick up any change made between construction and subscription
            Rebuild();
        }

        private void Rebuild()
        {
            _current.Value = Build();
        }

        private ModeHudViewModel Build()
        {
            return ModeHudViewModelFactory.Create(_uiStateControl.CurrentState, _placeSystemStateController.CurrentTarget, _placementHeightOffset.Value);
        }
    }
}
