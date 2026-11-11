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
    /// 指定画面の UiRoutingState を組む
    /// Builds a UiRoutingState on the given screen
    /// </summary>
    public static class UiRoutingStateTestFactory
    {
        public static UiRoutingState Create(GameObject host, UIStateEnum state)
        {
            return CreateSwitchable(host, state, new[] { state }, out _);
        }

        // 指定の画面群の間を Transit で行き来できる UiRoutingState を組む
        // Builds a UiRoutingState that can move between the given screens via Transit
        public static UiRoutingState CreateSwitchable(GameObject host, UIStateEnum initial, IReadOnlyList<UIStateEnum> states, out UIStateControl control)
        {
            control = host.AddComponent<UIStateControl>();
            control.Construct(DictionaryWith(), new FakePlayerObjectController());
            control.Initialize(initial, new UITransitContext(initial));
            var routing = new UiRoutingState(control, Array.Empty<INestedPauseScreenState>());
            routing.Initialize();
            return routing;

            #region Internal

            UIStateDictionary DictionaryWith()
            {
                // UIStateControlTest.CreateDictionary と同じ反射で、指定の画面だけを入れる
                // Same reflection as UIStateControlTest.CreateDictionary, holding only the given screens
                var result = new UIStateDictionary(null, null, null, null, null, null, null, null, null, null, null, null);
                var field = typeof(UIStateDictionary).GetField("_stateDictionary", BindingFlags.Instance | BindingFlags.NonPublic);
                var dictionary = (Dictionary<UIStateEnum, IUIState>)field.GetValue(result);
                foreach (var state in states) dictionary[state] = new IdleUiState();
                return result;
            }

            #endregion
        }

        // 遷移を要求し Update を 1 回回して確定する（UIStateControlTest.InvokeUpdate と同じ反射）
        // Requests a transition and runs one Update to settle it (same reflection as UIStateControlTest.InvokeUpdate)
        public static void Transit(UIStateControl control, UIStateEnum next)
        {
            control.RequestTransition(next);
            typeof(UIStateControl).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(control, null);
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
