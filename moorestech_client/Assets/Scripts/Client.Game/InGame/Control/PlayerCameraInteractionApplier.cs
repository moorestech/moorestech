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

        public void SetCursorVisible(bool visible)
        {
            InputManager.MouseCursorVisible(visible);
        }

        public void SetCameraRotatable(bool rotatable)
        {
            _inGameCameraController.SetControllable(rotatable);
        }
    }
}
