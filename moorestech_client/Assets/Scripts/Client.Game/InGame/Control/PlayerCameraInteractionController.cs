using Client.Input;

namespace Client.Game.InGame.Control
{
    public class PlayerCameraInteractionController
    {
        private readonly InGameCameraController _inGameCameraController;

        public PlayerCameraInteractionController(InGameCameraController inGameCameraController)
        {
            _inGameCameraController = inGameCameraController;
        }

        public void EnterGameplay()
        {
            ApplyCursorAndRotation(false, true);
        }

        public void EnterCursorInteraction()
        {
            ApplyCursorAndRotation(true, false);
        }

        public void UpdateRightDrag()
        {
            if (HybridInput.GetMouseButtonDown(1)) ApplyCursorAndRotation(false, true);
            if (HybridInput.GetMouseButtonUp(1)) ApplyCursorAndRotation(true, false);
        }

        public void ExitCursorInteraction()
        {
            // MouseUpを取り逃しても次の操作状態へドラッグ状態を持ち越さない
            // Do not carry drag state into the next interaction when MouseUp was missed
            ApplyCursorAndRotation(true, false);
        }

        private void ApplyCursorAndRotation(bool cursorVisible, bool cameraRotatable)
        {
            InputManager.MouseCursorVisible(cursorVisible);
            _inGameCameraController.SetControllable(cameraRotatable);
        }
    }
}
