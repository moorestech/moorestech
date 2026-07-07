namespace Client.Game.InGame.Control.BuildView
{
    /// <summary>
    ///     視点モード切替の副作用（カメラ・カーソル・クロスヘア・自機表示）を適用する
    ///     Applies view-mode side effects (camera, cursor, crosshair, player model)
    /// </summary>
    public interface IBuildViewApplier
    {
        TweenCameraInfo CaptureCurrentCamera();
        void ApplyTopDownCamera();
        void RestoreCamera(TweenCameraInfo saved);
        void SetFirstPersonCamera(bool enabled);
        void SetCursorVisible(bool visible);
        void SetCrosshairVisible(bool visible);
        void SetCameraRotatable(bool rotatable);
    }
}
