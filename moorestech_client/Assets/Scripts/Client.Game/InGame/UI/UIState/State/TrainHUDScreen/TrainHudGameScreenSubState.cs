using System.Collections.Generic;
using Client.Game.InGame.Control;
using Client.Input;
using Mooresmaster.Localization.Generated;

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

        public IReadOnlyList<KeyHint> GetKeyHints()
        {
            return TrainHudGameScreenSubStateHints.Hints;
        }
    }

    internal static class TrainHudGameScreenSubStateHints
    {
        public static readonly IReadOnlyList<KeyHint> Hints = new[]
        {
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.DriveKeys, LocalizationKeys.Ui.KeyHint.Text.TrainDrive),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.BranchKeys, LocalizationKeys.Ui.KeyHint.Text.TrainSelectBranch),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.E, LocalizationKeys.Ui.KeyHint.Text.TrainDismount),
        };
    }
}
