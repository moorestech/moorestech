using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using UniRx;

namespace Client.Game.InGame.UI.UIState.State.NestedPause
{
    // 入れ子ポーズを持つ画面のステートマシン。UIStateControlの簡易版
    // Nested state machine for screens that own a pause menu. A simplified counterpart of UIStateControl
    public class NestedPauseSubStateController
    {
        private readonly Dictionary<NestedPauseSubStateEnum, INestedPauseSubState> _states;
        private readonly Subject<NestedPauseSubStateEnum> _onStateChanged = new();
        
        public NestedPauseSubStateEnum CurrentState { get; private set; }
        public IObservable<NestedPauseSubStateEnum> OnStateChanged => _onStateChanged;
        
        public NestedPauseSubStateController(INestedPauseSubState gameScreenSubState, PauseMenuStateService pauseMenuStateService)
        {
            _states = new Dictionary<NestedPauseSubStateEnum, INestedPauseSubState>
            {
                { NestedPauseSubStateEnum.GameScreen, gameScreenSubState },
                { NestedPauseSubStateEnum.PauseMenuScreen, new PauseMenuNestedSubState(pauseMenuStateService) },
            };
        }
        
        public void StartSubState()
        {
            CurrentState = NestedPauseSubStateEnum.GameScreen;
            _states[CurrentState].OnEnter();
            _onStateChanged.OnNext(CurrentState);
        }
        
        public void Update()
        {
            var next = _states[CurrentState].GetNextUpdate();
            if (next == null) return;
            
            Transit(next.Value);
        }
        
        public IReadOnlyList<KeyHint> GetKeyHints()
        {
            return _states[CurrentState].GetKeyHints();
        }
        
        // Web側の閉じ要求。実際に閉じたときだけtrueを返す
        // Close request from the web side. Returns true only when the pause menu actually closed
        public bool RequestClosePauseMenu()
        {
            if (CurrentState != NestedPauseSubStateEnum.PauseMenuScreen) return false;
            
            Transit(NestedPauseSubStateEnum.GameScreen);
            return true;
        }
        
        // 画面終了時に呼ぶ。開いていればポーズを閉じ、公開値をGameScreenへ戻す
        // Called when the screen ends; closes the pause menu and resets the exposed sub-state to GameScreen
        public void ShutdownSubState()
        {
            _states[CurrentState].OnExit();
            CurrentState = NestedPauseSubStateEnum.GameScreen;
            _onStateChanged.OnNext(CurrentState);
        }
        
        // アプリのフォーカス復帰。GameScreenに居るときだけ入場処理をやり直す
        // Application focus regained; re-runs the entry work only while sitting on GameScreen
        public void RestoreAfterApplicationFocus()
        {
            if (CurrentState != NestedPauseSubStateEnum.GameScreen) return;
            _states[CurrentState].OnEnter();
        }
        
        private void Transit(NestedPauseSubStateEnum next)
        {
            _states[CurrentState].OnExit();
            CurrentState = next;
            _states[CurrentState].OnEnter();
            _onStateChanged.OnNext(CurrentState);
        }
    }
}
