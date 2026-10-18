namespace Client.Game.InGame.Block.Preview
{
    /// <summary>
    /// 既設ブロックのプレビュー専用表示の出し方
    /// How a placed block shows its preview-only objects
    /// </summary>
    public enum PreviewOnlyObjectsDisplay
    {
        // 非アクティブ（当たり判定も無し）
        // Inactive, no colliders either
        Hidden,
        // 当たり判定だけ有効で描画しない（設置アニメーション中）
        // Colliders active but not rendered (during place animation)
        CollidersOnly,
        // 距離に関係なく描画（デバッグのホバー）
        // Rendered regardless of distance (debug hover)
        Rendered,
        // 当たり判定は有効のまま、プレイヤー半径内だけ描画（設置モード）
        // Colliders stay active, rendered only within the player radius (build mode)
        RenderedWithinPlayerRadius,
    }
}
