using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Crosshair;
using Client.Input;

namespace Client.Game.InGame.Control.ViewMode
{
    /// <summary>
    ///     視点モードの副作用を実機へ適用する
    ///     Applies view-mode side effects to the camera, cursor, crosshair, and player model
    /// </summary>
    public class PlayerViewApplier : IPlayerViewApplier
    {
        private readonly InGameCameraController _inGameCameraController;

        public PlayerViewApplier(InGameCameraController inGameCameraController)
        {
            _inGameCameraController = inGameCameraController;
        }

        public void SetFirstPersonCamera(bool enabled)
        {
            // FPS化と自機モデルの非表示を一括切替する（視点回転はSetCameraRotatableが担当）
            // Toggle the FPS camera together with player model visibility (look rotation is owned by SetCameraRotatable)
            _inGameCameraController.SetFirstPersonMode(enabled);
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
