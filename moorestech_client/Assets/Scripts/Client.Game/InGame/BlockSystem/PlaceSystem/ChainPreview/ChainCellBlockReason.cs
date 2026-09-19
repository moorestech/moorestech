namespace Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview
{
    /// <summary>
    ///     連結セルが置けない原因。文言は原因ごとに分かれる
    ///     Why a chain cell cannot be placed; the tooltip wording differs per reason
    /// </summary>
    public enum ChainCellBlockReason
    {
        // 置ける
        // The cell is placeable
        None,

        // 既存ブロックと重なっている
        // The cell overlaps an existing block
        OverlappingBlock,

        // 地表が取れないセル
        // No ground could be resolved under the cell
        GroundNotFound,

        // 地表はあるが設置Yと合わない（埋まり/浮き）
        // Ground exists but its height disagrees with the placement Y (buried or floating)
        GroundHeightMismatch,

        // 上下向き設置で連結ゴーストの向きが12方位に収まらない
        // Facing up/down rotates a chain ghost outside the 12 directions
        VerticalAnchor,
    }
}
