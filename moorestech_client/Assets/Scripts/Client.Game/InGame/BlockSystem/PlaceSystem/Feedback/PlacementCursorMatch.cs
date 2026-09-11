namespace Client.Game.InGame.BlockSystem.PlaceSystem.Feedback
{
    /// <summary>
    ///     ドラッグ列からカーソル下セルを選ぶ規則。どちらを使うかは設置系ごとに呼び出し側が指定する
    ///     The rule that picks the cell under the cursor from a drag; each placement system states which one it wants
    /// </summary>
    public enum PlacementCursorMatch
    {
        /// <summary>
        ///     セル座標の完全一致で引き、無ければ末尾セル
        ///     Matches the exact cell position, falling back to the last cell
        /// </summary>
        ExactCellOrLast,

        /// <summary>
        ///     完全一致→XZ一致→末尾セルの順で引く。列のYがカーソルのYと揃わない設置系向け
        ///     Matches the exact cell, then XZ, then the last cell; for systems whose cells sit at other heights than the cursor
        /// </summary>
        HorizontalCellOrLast,
    }
}
