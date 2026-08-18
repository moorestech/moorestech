namespace Client.Game.InGame.Control
{
    public interface IPlayerCameraInteractionApplier
    {
        void SetInteractionMode(CameraInteractionMode mode);

        // ワープは状態でなく1回限りの動作のため、SetInteractionModeの引数に混ぜず別メソッドにする
        // A warp is a one-shot action, not a state, so it stays separate from SetInteractionMode's argument
        void WarpCursorToScreenCenter();
    }
}
