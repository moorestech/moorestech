using Client.Game.InGame.Tutorial;
using Client.Game.InGame.UI.UIState;
using Client.Ui.State.SkitPresence;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Tutorial.KeyControlHint
{
    public sealed class KeyControlHintModel : IInitializable
    {
        private readonly UIStateControl _uiStateControl;
        private readonly BlockingSkitPresence _blockingSkit;
        private readonly TutorialPresentationStateStore _store;
        private readonly KeyControlHintProjection _projection = new();
        private readonly ReactiveProperty<KeyControlHintViewModel> _current = new(KeyControlHintViewModel.Empty);

        public IReadOnlyReactiveProperty<KeyControlHintViewModel> Current => _current;

        public KeyControlHintModel(UIStateControl uiStateControl, BlockingSkitPresence blockingSkit, TutorialPresentationStateStore store)
        {
            _uiStateControl = uiStateControl;
            _blockingSkit = blockingSkit;
            _store = store;
        }

        public void Initialize()
        {
            // 演出・画面状態・スキットのどれが変わっても組み直す
            // Rebuild whenever the presentation, the UI state or the skit changes
            _store.ObserveChanged().Subscribe(_ => Rebuild());
            _uiStateControl.OnStateChanged.Subscribe(_ => Rebuild());
            _blockingSkit.IsBlocking.Skip(1).Subscribe(_ => Rebuild());

            // ctor から購読までの間の変化を取り込む
            // Pick up any change made between construction and subscription
            Rebuild();
        }

        private void Rebuild()
        {
            // UiScreen に無い状態（GameScreen 等。マスタが UIStateEnum の名前で指す）なので UIStateEnum を読む（A-248）
            // These states (GameScreen etc.; the master names them by UIStateEnum) are not in UiScreen, so read UIStateEnum (A-248)
            _current.Value = _projection.Project(_store.GetCurrent(), _uiStateControl.CurrentState, _blockingSkit.IsBlocking.Value);
        }
    }
}
