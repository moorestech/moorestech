using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Game.Skit;
using UniRx;

namespace Client.Game.InGame.UI.UIState.State.Skit
{
    // スキット画面用の入れ子ステートマシン
    // Nested state machine for the skit screen
    public class SkitScreenUIStateController
    {
        private readonly Dictionary<SkitScreenUIStateEnum, ISkitScreenSubState> _states;
        private readonly Subject<SkitScreenUIStateEnum> _onStateChanged = new();
        
        public SkitScreenUIStateEnum CurrentState { get; private set; }
        public IObservable<SkitScreenUIStateEnum> OnStateChanged => _onStateChanged;
        
        public SkitScreenUIStateController(SkitManager skitManager, PauseMenuStateService pauseMenuStateService)
        {
            _states = new Dictionary<SkitScreenUIStateEnum, ISkitScreenSubState>
            {
                { SkitScreenUIStateEnum.Playing, new SkitPlayingSubState(skitManager) },
                { SkitScreenUIStateEnum.PauseMenu, new SkitPauseMenuSubState(pauseMenuStateService) },
            };
        }
        
        public void StartSubState()
        {
            CurrentState = SkitScreenUIStateEnum.Playing;
            _states[CurrentState].OnEnter();
            _onStateChanged.OnNext(CurrentState);
        }
        
        public void Update()
        {
            var next = _states[CurrentState].GetNextUpdate();
            if (next == null) return;
            
            _states[CurrentState].OnExit();
            CurrentState = next.Value;
            _states[CurrentState].OnEnter();
            _onStateChanged.OnNext(CurrentState);
        }
        
        // Web側の閉じ要求。入れ子だけ閉じスキット継続
        // Close request from the web side. Only the nested state closes; the skit continues
        public void RequestClosePauseMenu()
        {
            if (CurrentState != SkitScreenUIStateEnum.PauseMenu) return;
            _states[CurrentState].OnExit();
            CurrentState = SkitScreenUIStateEnum.Playing;
            _states[CurrentState].OnEnter();
            _onStateChanged.OnNext(CurrentState);
        }
        
        // スキット終了時に呼ぶ（開いていれば閉じる）
        // Called when the skit ends (closes the pause menu if open)
        public void ShutdownSubState()
        {
            _states[CurrentState].OnExit();
            
            // 終了後に入れ子stateがPauseMenuのまま残らないようPlayingへ戻す（SubState公開値の正しさを保つ）
            // Reset to Playing so the nested state does not linger on PauseMenu after shutdown (keeps the exposed SubState truthful)
            CurrentState = SkitScreenUIStateEnum.Playing;
            _onStateChanged.OnNext(CurrentState);
        }
    }
}
