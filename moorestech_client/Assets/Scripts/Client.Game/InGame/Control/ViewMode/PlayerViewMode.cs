namespace Client.Game.InGame.Control.ViewMode
{
    /// <summary>
    ///     プレイヤーの視点モード。ゲーム中は常に保持され、Vキーでいつでも切り替えられる
    ///     The player's view mode, kept for the whole session and toggleable anytime with the V key
    /// </summary>
    public enum PlayerViewMode
    {
        ThirdPerson,
        FirstPerson,
    }
}
