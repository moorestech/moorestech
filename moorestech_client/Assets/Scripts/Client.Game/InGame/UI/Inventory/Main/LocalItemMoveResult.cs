namespace Client.Game.InGame.UI.Inventory.Main
{
    /// <summary>
    /// コントローラの移動（MoveItem）の結果。何もしない移動と拒否もここで分ける。足すと受け手の switch が網羅漏れで落ちる
    /// Result of the controller's move (MoveItem), telling apart no-effect moves and denials; adding one breaks the receivers' exhaustive switches
    /// </summary>
    public enum LocalItemMoveResult
    {
        Moved,
        NoEffect,
        SourceEmpty,
        SourceCountShort,
    }
}
