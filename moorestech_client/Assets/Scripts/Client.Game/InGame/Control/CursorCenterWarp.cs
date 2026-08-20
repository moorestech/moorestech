namespace Client.Game.InGame.Control
{
    /// <summary>
    ///     カーソル解放時に中央へ寄せるかを呼び出し側が明示する契約
    ///     Lets callers state explicitly whether a freed cursor is placed at the screen center
    /// </summary>
    public enum CursorCenterWarp
    {
        None,
        ToScreenCenter,
    }
}
