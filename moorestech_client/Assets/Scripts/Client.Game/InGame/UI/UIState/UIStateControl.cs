using System;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.UIState
{
    public class UIStateControl : MonoBehaviour
    {
        [Inject] private UIStateDictionary _uiStateDictionary;

        public event Action<UIStateEnum> OnStateChanged;
        public UIStateEnum CurrentState { get; private set; }

        private UIStateEnum? _webTransitionRequest;
        private bool _lastWebUiMode;

        public void Initialize(UIStateEnum initialState, UITransitContext initialContext)
        {
            CurrentState = initialState;
            _uiStateDictionary.GetState(CurrentState).OnEnter(initialContext);
            WebUiScreenGate.SetCurrentUiState(CurrentState);
        }

        // Web UI からの遷移要求を受け付ける（次のUpdateで最優先消費）
        // Accept a transition request from the Web UI (consumed first in the next Update)
        public void RequestTransition(UIStateEnum nextState)
        {
            _webTransitionRequest = nextState;
        }

        // UI state
        private void Update()
        {
            // webモード終了の立ち下がりでGameScreenへ正規化しカーソル・カメラを復元する
            // On the web-mode falling edge, normalize to GameScreen to restore cursor/camera
            var webUiMode = WebUiScreenGate.IsWebUiMode;
            if (_lastWebUiMode && !webUiMode)
            {
                _lastWebUiMode = webUiMode;
                _webTransitionRequest = null;
                ForceReturnToGameScreen();
                return;
            }
            _lastWebUiMode = webUiMode;

            // Web要求を最優先で消費し、無ければ現stateの入力判定を使う
            // Consume the web request first; otherwise poll the current state's input
            var nextContext = ConsumeWebRequest() ?? _uiStateDictionary.GetState(CurrentState).GetNextUpdate();
            if (nextContext == null) return;

            // webモード中はWeb未実装stateへの遷移を抑止する（不可視UIへの閉じ込め防止）
            // While in web mode, suppress transitions to web-unimplemented states (avoid invisible-UI traps)
            if (webUiMode && !WebUiScreenGate.IsWebSupportedState(nextContext.NextStateEnum)) return;

            var lastState = CurrentState;
            nextContext.SetLastState(lastState);
            CurrentState = nextContext.NextStateEnum;

            //現在のUIステートを終了し、次のステートを呼び出す
            // Exit current UI state and call next state
            _uiStateDictionary.GetState(lastState).OnExit();
            _uiStateDictionary.GetState(CurrentState).OnEnter(nextContext);

            WebUiScreenGate.SetCurrentUiState(CurrentState);
            OnStateChanged?.Invoke(CurrentState);

            #region Internal

            UITransitContext ConsumeWebRequest()
            {
                if (_webTransitionRequest == null) return null;
                var requested = _webTransitionRequest.Value;
                _webTransitionRequest = null;

                // 同一stateへの要求は遷移不要
                // A request for the current state needs no transition
                if (requested == CurrentState) return null;
                return new UITransitContext(requested);
            }

            #endregion
        }

        private void ForceReturnToGameScreen()
        {
            // GameScreen以外なら終了処理を呼んでパネル等を閉じる
            // If not GameScreen, run its exit to close panels etc.
            var lastState = CurrentState;
            if (lastState != UIStateEnum.GameScreen) _uiStateDictionary.GetState(lastState).OnExit();

            // GameScreenへ再入場しカーソル・カメラ・操作説明を確定させる（同一状態でもカーソル復元のため実行）
            // Re-enter GameScreen to settle cursor/camera/key description (run even for the same state to restore cursor)
            CurrentState = UIStateEnum.GameScreen;
            _uiStateDictionary.GetState(CurrentState).OnEnter(new UITransitContext(UIStateEnum.GameScreen));

            WebUiScreenGate.SetCurrentUiState(CurrentState);
            if (lastState != CurrentState) OnStateChanged?.Invoke(CurrentState);
        }
    }
}
