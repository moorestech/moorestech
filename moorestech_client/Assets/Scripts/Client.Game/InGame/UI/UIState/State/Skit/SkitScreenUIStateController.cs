using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Game.Skit;
using UniRx;

namespace Client.Game.InGame.UI.UIState.State.Skit
{
    // スキット画面専用の入れ子ステートマシン。TrainHudScreenUIStateControllerと同型
    // Nested state machine dedicated to the skit screen. Same shape as TrainHudScreenUIStateController
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
        
        // Web側のポーズメニュー閉じ要求。列車HUDと同じく入れ子だけを閉じスキットは続行する
        // Close request from the web pause menu. Like the train HUD, only the nested state closes and the skit continues
        public void RequestClosePauseMenu()
        {
            if (CurrentState != SkitScreenUIStateEnum.PauseMenu) return;
            _states[CurrentState].OnExit();
            CurrentState = SkitScreenUIStateEnum.Playing;
            _states[CurrentState].OnEnter();
            _onStateChanged.OnNext(CurrentState);
        }
        
        // スキット終了時に呼ぶ。メニューが開いていれば閉じる（ADR 0035: 終了時はGameScreenへ）
        // Called when the skit ends. Closes the pause menu if open (ADR 0035: return to GameScreen on end)
        public void ShutdownSubState()
        {
            _states[CurrentState].OnExit();
        }
    }
}
