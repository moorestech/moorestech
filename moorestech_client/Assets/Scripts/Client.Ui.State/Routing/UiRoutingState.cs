using System.Collections.Generic;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.NestedPause;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Routing
{
    /// <summary>
    /// 現在の画面を購読で保つ。どちらの系で描くかは持たない（全体スイッチは composition root だけが読む。設計書 §4.4）
    /// Keeps the current screen via subscriptions; it does not know which system draws (only the composition root reads the global switch; design §4.4)
    /// </summary>
    public sealed class UiRoutingState : IInitializable
    {
        public IReadOnlyReactiveProperty<UiRoutingSnapshot> Current => _current;

        private readonly UIStateControl _uiStateControl;
        private readonly IReadOnlyList<INestedPauseScreenState> _nestedPauseScreens;
        private readonly ReactiveProperty<UiRoutingSnapshot> _current;

        public UiRoutingState(UIStateControl uiStateControl, IReadOnlyList<INestedPauseScreenState> nestedPauseScreens)
        {
            _uiStateControl = uiStateControl;
            _nestedPauseScreens = nestedPauseScreens;
            _current = new ReactiveProperty<UiRoutingSnapshot>(Resolve());
        }

        public void Initialize()
        {
            // state 遷移と入れ子画面の表示変化で解決し直す。同値は ReactiveProperty が落とす
            // Re-resolve on state transitions and nested-screen presentation changes; ReactiveProperty drops equal values
            _uiStateControl.OnStateChanged.Subscribe(_ => Refresh());
            foreach (var nested in _nestedPauseScreens) nested.OnPresentationChanged.Subscribe(_ => Refresh());
            Refresh();
        }

        private void Refresh()
        {
            _current.Value = Resolve();
        }

        private UiRoutingSnapshot Resolve()
        {
            var subState = _uiStateControl.GetCurrentNestedPauseScreen()?.SubState;
            return new UiRoutingSnapshot(UiScreenRouting.Resolve(_uiStateControl.CurrentState, subState));
        }
    }

    /// <summary>
    /// 画面ルーティングの不変スナップショット
    /// Immutable snapshot of screen routing
    /// </summary>
    public sealed class UiRoutingSnapshot
    {
        public UiScreen Screen { get; }

        public UiRoutingSnapshot(UiScreen screen)
        {
            Screen = screen;
        }

        public override bool Equals(object obj)
        {
            return obj is UiRoutingSnapshot other && Screen == other.Screen;
        }

        public override int GetHashCode()
        {
            return (int)Screen;
        }

        public override string ToString()
        {
            return Screen.ToString();
        }
    }
}
