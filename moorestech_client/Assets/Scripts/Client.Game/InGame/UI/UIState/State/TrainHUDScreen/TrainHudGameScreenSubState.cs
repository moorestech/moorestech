using Client.Game.InGame.Control;
using Client.Game.InGame.UI.KeyControl;
using Client.Input;

namespace Client.Game.InGame.UI.UIState.State.TrainHUDScreen
{
    // 列車操作中のサブステート。Eで降車・WASDで操作・Escでポーズへ
    // Sub-state while actively driving the train. E to dismount, WASD to control, Esc to pause.
    public class TrainHudGameScreenSubState : ITrainHudScreenSubState
    {
        private readonly InGameCameraController _inGameCameraController;

        public TrainHudGameScreenSubState(InGameCameraController inGameCameraController)
        {
            _inGameCameraController = inGameCameraController;
        }

        public void OnEnter()
        {
            // 操作中はカメラを動かし、カーソルを隠す
            // While driving, enable camera control and hide the cursor.
            _inGameCameraController.SetControllable(true);
            InputManager.MouseCursorVisible(false);
            KeyControlDescription.Instance.SetText("E: 降車\nW/A/S/D: 列車操作\nEsc: メニュー\n");
        }

        public TrainHudScreenUIStateEnum? GetNextUpdate()
        {
            // Escでポーズメニューへ遷移
            // Esc transits to the pause menu.
            if (InputManager.UI.OpenMenu.GetKeyDown) return TrainHudScreenUIStateEnum.PauseMenuScreen;
            return null;
        }

        public void OnExit()
        {
            _inGameCameraController.SetControllable(false);
        }
    }
}
