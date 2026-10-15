namespace Client.Game.InGame.BlockSystem.PlaceSystem.Ground
{
    /// <summary>
    ///     地形ゲート付き設置系のカーソルセル解決の結果。照準面なし・地表なし・解決済みを区別する
    ///     Result of the terrain-gated placements' cursor cell resolution; separates no aim surface, no ground and resolved
    /// </summary>
    public enum FootprintGroundedCellResult
    {
        NoPlacementSurface,
        GroundNotFound,
        Resolved,
    }
}
