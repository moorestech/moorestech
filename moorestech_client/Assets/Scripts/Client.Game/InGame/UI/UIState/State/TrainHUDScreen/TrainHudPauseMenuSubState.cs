using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Input;

namespace Client.Game.InGame.UI.UIState.State.TrainHUDScreen
{
    // ポーズメニュー表示中のサブステート。Escで閉じてGameScreenへ戻る
    // Sub-state while the pause menu is showing. Esc closes the menu and returns to GameScreen.
    public class TrainHudPauseMenuSubState : ITrainHudScreenSubState
    {
        private readonly PauseMenuStateService _pauseMenuStateService;

        public TrainHudPauseMenuSubState(PauseMenuStateService pauseMenuStateService)
        {
            _pauseMenuStateService = pauseMenuStateService;
        }

        public void OnEnter()
        {
            _pauseMenuStateService.OnEnter();
        }

        public TrainHudScreenUIStateEnum? GetNextUpdate()
        {
            return _pauseMenuStateService.IsClosePause() ? TrainHudScreenUIStateEnum.GameScreen : null;
        }

        public void OnExit()
        {
            _pauseMenuStateService.OnExit();
        }
    }
}
