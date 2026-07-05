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

        private bool _cefGateWasActive;

        public void Initialize(UIStateEnum initialState, UITransitContext initialContext)
        {
            CurrentState = initialState;
            _uiStateDictionary.GetState(CurrentState).OnEnter(initialContext);
        }

        // UI state
        private void Update()
        {
            // CEF WebUI表示中はゲーム側UIStateの遷移を停止する（静的ゲートを毎フレームポーリング）
            // While the CEF WebUI is shown, suppress game-side UIState transitions (poll the static gate each frame)
            if (WebUiScreenGate.IsCefActive)
            {
                _cefGateWasActive = true;
                return;
            }

            // CEFからuGUIへ戻った直後はGameScreenへ強制遷移しカーソル・カメラ操作を復元する
            // Right after returning from CEF to uGUI, force GameScreen to restore cursor/camera control
            if (_cefGateWasActive)
            {
                _cefGateWasActive = false;
                ForceReturnToGameScreen();
                return;
            }

            // 更新チェック
            // Check for updates
            var nextContext = _uiStateDictionary.GetState(CurrentState).GetNextUpdate();
            if (nextContext == null) return;

            var lastState = CurrentState;
            nextContext.SetLastState(lastState);
            CurrentState = nextContext.NextStateEnum;

            //現在のUIステートを終了し、次のステートを呼び出す
            // Exit current UI state and call next state
            _uiStateDictionary.GetState(lastState).OnExit();
            _uiStateDictionary.GetState(CurrentState).OnEnter(nextContext);

            OnStateChanged?.Invoke(CurrentState);
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

            if (lastState != CurrentState) OnStateChanged?.Invoke(CurrentState);
        }
    }
}