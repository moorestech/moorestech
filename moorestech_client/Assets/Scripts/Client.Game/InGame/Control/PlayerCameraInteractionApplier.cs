using Client.Input;

namespace Client.Game.InGame.Control
{
    public class PlayerCameraInteractionApplier : IPlayerCameraInteractionApplier
    {
        private readonly InGameCameraController _inGameCameraController;

        public PlayerCameraInteractionApplier(InGameCameraController inGameCameraController)
        {
            _inGameCameraController = inGameCameraController;
        }

        public void SetInteractionMode(CameraInteractionMode mode)
        {
            var cameraLook = mode == CameraInteractionMode.CameraLook;
            InputManager.MouseCursorVisible(!cameraLook);
            _inGameCameraController.SetControllable(cameraLook);
        }

        public void WarpCursorToScreenCenter()
        {
            InputManager.WarpMouseCursorToScreenCenter();
        }
    }
}
