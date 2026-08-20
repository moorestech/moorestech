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

        public void SetInteractionMode(CameraInteractionMode mode, CursorCenterWarp warp)
        {
            var cameraLook = mode == CameraInteractionMode.CameraLook;

            // 解放後のカーソル出現位置はOS任せのため、中央指定時はロック解除を終えてから寄せる
            // The freed cursor's position is OS-dependent, so center it only after the unlock has landed
            InputManager.MouseCursorVisible(!cameraLook);
            if (!cameraLook && warp == CursorCenterWarp.ToScreenCenter) InputManager.WarpMouseCursorToScreenCenter();

            _inGameCameraController.SetControllable(cameraLook);
        }
    }
}
