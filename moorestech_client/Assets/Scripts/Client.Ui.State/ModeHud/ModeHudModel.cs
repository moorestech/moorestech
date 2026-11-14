using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.ModeHud
{
    /// <summary>
    /// HUD の表示状態を購読して配る
    /// Subscribes to and publishes the HUD state
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
            // 変化の源 4 本（言語でも組み直す）
            // Four change sources (rebuild on language too)
            _uiStateControl.OnStateChanged.Subscribe(_ => Rebuild());
            _placeSystemStateController.OnTargetChanged.Subscribe(_ => Rebuild());
            _placementHeightOffset.OnChanged.Subscribe(_ => Rebuild());
            Localize.OnLanguageChanged.Subscribe(_ => Rebuild());

            // 購読までの間の変化を取り込む
            // Pick up changes made before subscribing
            Rebuild();
        }

        private void Rebuild()
        {
            _current.Value = Build();
        }

        private ModeHudViewModel Build()
        {
            // UiScreen に無い状態（PlaceBlock・DeleteBar）なので UIStateEnum を読む（A-248）
            // These states (PlaceBlock, DeleteBar) are not in UiScreen, so read UIStateEnum (A-248)
            return ModeHudViewModelFactory.Create(_uiStateControl.CurrentState, _placeSystemStateController.CurrentTarget, _placementHeightOffset.Value);
        }
    }
}
