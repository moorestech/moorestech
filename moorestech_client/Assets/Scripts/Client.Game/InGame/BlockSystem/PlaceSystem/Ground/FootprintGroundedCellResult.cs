namespace Client.Game.InGame.BlockSystem.PlaceSystem.Ground
{
    /// <summary>
    ///     - 地形ゲート付きのセル解決結果
    ///     - 照準面なし・地表なしを区別
    ///     - Cell resolution for terrain-gated placement
    ///     - Distinguishes no aim surface from missing ground
    /// </summary>
    public enum FootprintGroundedCellResult
    {
        NoPlacementSurface,
        GroundNotFound,
        Resolved,
    }
}
