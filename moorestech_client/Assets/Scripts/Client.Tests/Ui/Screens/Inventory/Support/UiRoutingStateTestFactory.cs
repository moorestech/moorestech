using System;
using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.NestedPause;
using Client.Tests.UIState.Fakes;
using Client.Ui.State.Routing;
using UnityEngine;

namespace Client.Tests.Ui.Screens.Inventory.Support
{
    /// <summary>
    /// 指定の画面に居る本物の UiRoutingState を組む（ホイールのルーターと Presenter の L1 用）
    /// Builds a real UiRoutingState on the given screen (for the wheel router and presenter L1)
    /// </summary>
    public static class UiRoutingStateTestFactory
    {
        public static UiRoutingState Create(GameObject host, UIStateEnum state)
        {
            var control = host.AddComponent<UIStateControl>();
            control.Construct(DictionaryWith(state), new FakePlayerObjectController());
            control.Initialize(state, new UITransitContext(state));
            var routing = new UiRoutingState(control, Array.Empty<INestedPauseScreenState>());
            routing.Initialize();
            return routing;

            #region Internal

            UIStateDictionary DictionaryWith(UIStateEnum only)
            {
                // UIStateControlTest.CreateDictionary と同じ反射で、指定の画面だけを入れる
                // Same reflection as UIStateControlTest.CreateDictionary, holding only the given screen
                var result = new UIStateDictionary(null, null, null, null, null, null, null, null, null, null, null, null);
                var field = typeof(UIStateDictionary).GetField("_stateDictionary", BindingFlags.Instance | BindingFlags.NonPublic);
                ((Dictionary<UIStateEnum, IUIState>)field.GetValue(result))[only] = new IdleUiState();
                return result;
            }

            #endregion
        }

        // 遷移を出さない画面
        // A screen that never requests a transition
        private sealed class IdleUiState : IUIState
        {
            public void OnEnter(UITransitContext context) { }
            public UITransitContext GetNextUpdate() { return null; }
            public void OnExit() { }
            public bool LocksPlayerMovement() { return false; }
            public bool AllowsSprintToggle() { return true; }
            public IReadOnlyList<KeyHint> GetKeyHints() { return Array.Empty<KeyHint>(); }
        }
    }
}
