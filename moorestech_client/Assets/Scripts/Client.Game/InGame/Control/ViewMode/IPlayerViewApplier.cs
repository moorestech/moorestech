namespace Client.Game.InGame.Control.ViewMode
{
    /// <summary>
    ///     視点モード切替の副作用を適用する
    ///     Applies view-mode side effects (camera, cursor, crosshair, player model)
    /// </summary>
    public interface IPlayerViewApplier
    {
        void SetFirstPersonCamera(bool enabled);
        void SetCursorVisible(bool visible);
        void SetCrosshairVisible(bool visible);
        void SetCameraRotatable(bool rotatable);
    }
}
