using System.Collections.Generic;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.NestedPause;
using Client.Ui.State.SkitPresence;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.KeyHints
{
    /// <summary>
    /// 現 UIState が宣言するヒントを読み、state 遷移・入れ子画面の表示変化・blocking スキットで作り直す（Web の ui.state topic の keyHints）
    /// Reads the hints the current UIState declares and rebuilds them on state transitions, nested-screen presentation changes and blocking skits (Web ui.state topic keyHints)
    /// </summary>
    public sealed class KeyHintHudModel : IInitializable
    {
        private readonly UIStateControl _uiStateControl;
        private readonly UIStateDictionary _uiStateDictionary;
        private readonly IReadOnlyList<INestedPauseScreenState> _nestedPauseScreens;
        private readonly BlockingSkitPresence _blockingSkitPresence;
        private readonly ReactiveProperty<KeyHintHudViewModel> _current;

        public IReadOnlyReactiveProperty<KeyHintHudViewModel> Current => _current;

        public KeyHintHudModel(UIStateControl uiStateControl, UIStateDictionary uiStateDictionary, IReadOnlyList<INestedPauseScreenState> nestedPauseScreens, BlockingSkitPresence blockingSkitPresence)
        {
            _uiStateControl = uiStateControl;
            _uiStateDictionary = uiStateDictionary;
            _nestedPauseScreens = nestedPauseScreens;
            _blockingSkitPresence = blockingSkitPresence;
            _current = new ReactiveProperty<KeyHintHudViewModel>(Build());
        }

        public void Initialize()
        {
            // 内容の正は state 側（ADR-0032）。変化の知らせごとに読み直す。ゲーム寿命なので購読は解かない
            // The state owns the content (ADR-0032); re-read on every change notice; it lives for the whole game, so subscriptions stay
            _uiStateControl.OnStateChanged.Subscribe(_ => Rebuild());
            foreach (var nested in _nestedPauseScreens) nested.OnPresentationChanged.Subscribe(_ => Rebuild());
            _blockingSkitPresence.IsBlocking.Skip(1).Subscribe(_ => Rebuild());
            Rebuild();

            #region Internal

            void Rebuild()
            {
                _current.SetValueAndForceNotify(Build());
            }

            #endregion
        }

        private KeyHintHudViewModel Build()
        {
            // UiScreen に無い状態（全 state のキーヒント・DeleteBar）なので UIStateEnum を読む（A-248）
            // These states (every state's key hints, DeleteBar) are not in UiScreen, so read UIStateEnum (A-248)
            var state = _uiStateControl.CurrentState;
            var hints = _uiStateDictionary.GetState(state).GetKeyHints();
            return KeyHintHudViewModel.Create(hints, state == UIStateEnum.DeleteBar, _blockingSkitPresence.IsBlocking.Value);
        }
    }
}
