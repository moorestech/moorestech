namespace Client.Game.InGame.Control
{
    public interface IPlayerCameraInteractionApplier
    {
        // ロック側の中央寄せはInputManagerが担うため、引数は解放側の配置だけを決める
        // InputManager owns the centering on the lock side, so the argument only places the freed cursor
        void SetInteractionMode(CameraInteractionMode mode, CursorCenterWarp warp);
    }
}
