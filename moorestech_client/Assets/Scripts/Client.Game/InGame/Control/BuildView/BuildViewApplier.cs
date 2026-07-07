using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Crosshair;
using Client.Game.InGame.UI.UIState.Input;
using Client.Input;

namespace Client.Game.InGame.Control.BuildView
{
    /// <summary>
    ///     視点モードの副作用を実機（カメラ・カーソル・クロスヘア・自機）へ適用する
    ///     Applies view-mode side effects to the camera, cursor, crosshair, and player model
    /// </summary>
    public class BuildViewApplier : IBuildViewApplier
    {
        private readonly InGameCameraController _inGameCameraController;

        public BuildViewApplier(InGameCameraController inGameCameraController)
        {
            _inGameCameraController = inGameCameraController;
        }

        public TweenCameraInfo CaptureCurrentCamera()
        {
            return _inGameCameraController.CreateCurrentCameraTweenCameraInfo();
        }

        public void ApplyTopDownCamera()
        {
            _inGameCameraController.StartTweenCamera(_inGameCameraController.CreateTopDownTweenCameraInfo());
        }

        public void RestoreCamera(TweenCameraInfo saved)
        {
            _inGameCameraController.StartTweenCamera(saved);
        }

        public void SetFirstPersonCamera(bool enabled)
        {
            // カメラFPS化・常時視点回転・自機非表示を一括で切り替える
            // Toggle FPS camera, always-on look rotation, and player model visibility together
            _inGameCameraController.SetFirstPersonMode(enabled);
            _inGameCameraController.SetControllable(enabled);
            PlayerSystemContainer.Instance.PlayerObjectController.SetModelVisible(!enabled);
        }

        public void SetCursorVisible(bool visible)
        {
            InputManager.MouseCursorVisible(visible);
        }

        public void SetCrosshairVisible(bool visible)
        {
            CrosshairView.Instance.SetVisible(visible);
        }

        public void SetCameraRotatable(bool rotatable)
        {
            _inGameCameraController.SetControllable(rotatable);
        }
    }
}
