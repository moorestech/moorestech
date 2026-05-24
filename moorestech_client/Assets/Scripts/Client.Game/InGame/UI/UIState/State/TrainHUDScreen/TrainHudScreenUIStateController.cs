using System.Collections.Generic;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.UIState.State.PauseMenu;

namespace Client.Game.InGame.UI.UIState.State.TrainHUDScreen
{
    // 列車HUD専用の入れ子ステートマシン。UIStateControlの簡易版
    // Nested state machine dedicated to the train HUD. A simplified counterpart of UIStateControl.
    public class TrainHudScreenUIStateController
    {
        private readonly Dictionary<TrainHudScreenUIStateEnum, ITrainHudScreenSubState> _states;

        public TrainHudScreenUIStateEnum CurrentState { get; private set; }

        public TrainHudScreenUIStateController(PauseMenuStateService pauseMenuStateService, InGameCameraController inGameCameraController)
        {
            _states = new Dictionary<TrainHudScreenUIStateEnum, ITrainHudScreenSubState>
            {
                { TrainHudScreenUIStateEnum.GameScreen, new TrainHudGameScreenSubState(inGameCameraController) },
                { TrainHudScreenUIStateEnum.PauseMenuScreen, new TrainHudPauseMenuSubState(pauseMenuStateService) },
            };
        }

        public void StartSubState()
        {
            CurrentState = TrainHudScreenUIStateEnum.GameScreen;
            _states[CurrentState].OnEnter();
        }

        public void Update()
        {
            var next = _states[CurrentState].GetNextUpdate();
            if (next == null) return;

            _states[CurrentState].OnExit();
            CurrentState = next.Value;
            _states[CurrentState].OnEnter();
        }

        public void ShutdownSubState()
        {
            _states[CurrentState].OnExit();
        }
    }

    public enum TrainHudScreenUIStateEnum
    {
        GameScreen,
        PauseMenuScreen,
    }
}
