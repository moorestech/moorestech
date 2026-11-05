using System.Collections.Generic;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.NestedPause;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Routing
{
    /// <summary>
    /// 現在の画面と Web 休止要否を購読で保つ
    /// Keeps the current screen and whether the web UI is suspended via subscriptions
    /// </summary>
    public sealed class UiRoutingState : IInitializable
    {
        public IReadOnlyReactiveProperty<UiRoutingSnapshot> Current => _current;

        private readonly UIStateControl _uiStateControl;
        private readonly UiOwnershipTable _ownershipTable;
        private readonly IReadOnlyList<INestedPauseScreenState> _nestedPauseScreens;
        private readonly ReactiveProperty<UiRoutingSnapshot> _current;

        public UiRoutingState(UIStateControl uiStateControl, UiOwnershipTable ownershipTable, IReadOnlyList<INestedPauseScreenState> nestedPauseScreens)
        {
            _uiStateControl = uiStateControl;
            _ownershipTable = ownershipTable;
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

        // Editor 上書きを変えたあとにも即時反映できるよう公開する
        // Public so an Editor override change can be applied immediately
        public void Refresh()
        {
            _current.Value = Resolve();
        }

        public UiRoutingSnapshot Resolve()
        {
            var subState = _uiStateControl.GetCurrentNestedPauseScreen()?.SubState;
            var screen = UiScreenRouting.Resolve(_uiStateControl.CurrentState, subState);

            // 段階3の拡張点: GameUi がモーダルを出している間もこの論理和に足して Web を休止させる
            // Stage 3 extension point: OR in "a GameUi modal is showing" here to suspend the web UI as well
            var webUiSuspended = _ownershipTable.GetOwner(screen, UiSurface.MainPanel) == UiSurfaceOwner.GameUi;
            return new UiRoutingSnapshot(screen, webUiSuspended);
        }
    }

    /// <summary>
    /// 画面ルーティングの不変スナップショット
    /// Immutable snapshot of screen routing
    /// </summary>
    public sealed class UiRoutingSnapshot
    {
        public UiScreen Screen { get; }
        public bool WebUiSuspended { get; }

        public UiRoutingSnapshot(UiScreen screen, bool webUiSuspended)
        {
            Screen = screen;
            WebUiSuspended = webUiSuspended;
        }

        public override bool Equals(object obj)
        {
            return obj is UiRoutingSnapshot other && Screen == other.Screen && WebUiSuspended == other.WebUiSuspended;
        }

        public override int GetHashCode()
        {
            return ((int)Screen * 2) + (WebUiSuspended ? 1 : 0);
        }

        public override string ToString()
        {
            return $"{Screen}(webUiSuspended={WebUiSuspended})";
        }
    }
}
