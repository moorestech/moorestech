namespace Client.Game.InGame.Block.Preview
{
    /// <summary>
    /// 既設ブロックのプレビュー専用表示の出し方
    /// How a placed block shows its preview-only objects
    /// </summary>
    public enum PreviewOnlyObjectsDisplay
    {
        // 非アクティブ
        // Inactive
        Hidden,
        // 距離に関係なく描画（デバッグのホバー）
        // Rendered regardless of distance (debug hover)
        Rendered,
        // プレイヤー半径内だけ有効にして描画（設置モード）
        // Active and rendered only within the player radius (build mode)
        RenderedWithinPlayerRadius,
    }
}
