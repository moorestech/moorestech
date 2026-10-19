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
        // 設置アニメ中は当たり判定のみ
        // Keep only colliders during placement animation
        CollidersOnly,
        // 距離に関係なく描画（デバッグのホバー）
        // Rendered regardless of distance (debug hover)
        Rendered,
        // 当たり判定は有効のまま、プレイヤー半径内だけ描画（設置モード）
        // Colliders stay active, rendered only within the player radius (build mode)
        RenderedWithinPlayerRadius,
    }
}
