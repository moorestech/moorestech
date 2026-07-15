using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Crosshair;

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

        public void SetViewMode(PlayerViewMode mode)
        {
            var isFirstPerson = mode == PlayerViewMode.FirstPerson;

            // 視点そのものに属する表示だけを同期する
            // Synchronize only presentation that belongs to the selected view mode
            _inGameCameraController.SetFirstPersonMode(isFirstPerson);
            PlayerSystemContainer.Instance.PlayerObjectController.SetModelVisible(!isFirstPerson);
            CrosshairView.Instance.SetVisible(isFirstPerson);
            AimPointProvider.SetViewMode(mode);
        }
    }
}
