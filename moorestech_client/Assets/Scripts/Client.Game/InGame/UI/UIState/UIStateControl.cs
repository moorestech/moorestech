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
        private bool? _lastWebUiMode;

        public void Initialize(UIStateEnum initialState, UITransitContext initialContext)
        {
            CurrentState = initialState;
            _uiStateDictionary.GetState(CurrentState).OnEnter(initialContext);
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
            // webモード切替の両エッジでGameScreenへ正規化する（uGUI/Webビューの表示不整合を防ぐ）
            // Normalize to GameScreen on both web-mode edges (prevents uGUI/web view visibility mismatch)
            var webUiMode = WebUiScreenGate.IsWebUiMode;
            if (_lastWebUiMode == null)
            {
                // 初回Updateは記録のみとし、起動時の偽エッジ正規化を防ぐ（乗車ログイン時のTrainHUD維持）
                // First Update only records the mode, preventing spurious boot-edge normalization (keeps TrainHUD on ride-login)
                _lastWebUiMode = webUiMode;
            }
            else if (webUiMode != _lastWebUiMode)
            {
                _lastWebUiMode = webUiMode;
                _webTransitionRequest = null;
                ForceReturnToGameScreen();
                return;
            }

            // Web要求を最優先で消費し、無ければ現stateの入力判定を使う
            // Consume the web request first; otherwise poll the current state's input
            var nextContext = ConsumeWebRequest() ?? _uiStateDictionary.GetState(CurrentState).GetNextUpdate();
            if (nextContext == null) return;

            var lastState = CurrentState;
            nextContext.SetLastState(lastState);
            CurrentState = nextContext.NextStateEnum;

            //現在のUIステートを終了し、次のステートを呼び出す
            // Exit current UI state and call next state
            _uiStateDictionary.GetState(lastState).OnExit();
            _uiStateDictionary.GetState(CurrentState).OnEnter(nextContext);

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

            void ForceReturnToGameScreen()
            {
                // GameScreen外は終了処理を実行
                // Run exit unless on GameScreen
                var lastState = CurrentState;
                if (lastState != UIStateEnum.GameScreen) _uiStateDictionary.GetState(lastState).OnExit();

                // GameScreenへ再入場しカーソル・カメラ・操作説明を確定させる（同一状態でもカーソル復元のため実行）
                // Re-enter GameScreen to settle cursor/camera/key description (run even for the same state to restore cursor)
                CurrentState = UIStateEnum.GameScreen;
                _uiStateDictionary.GetState(CurrentState).OnEnter(new UITransitContext(UIStateEnum.GameScreen));

                if (lastState != CurrentState) OnStateChanged?.Invoke(CurrentState);
            }

            #endregion
        }
    }
}
