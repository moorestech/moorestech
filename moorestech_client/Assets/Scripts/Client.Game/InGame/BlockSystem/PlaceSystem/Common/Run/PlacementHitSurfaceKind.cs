namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run
{
    /// <summary>
    ///     設置レイが当たった面の種別
    ///     The kind of surface hit by the placement ray
    ///     地形追従の可否はこの種別で決まるため、boolのまま貫通させない
    ///     Terrain following is decided from this kind, so it never travels as a bare bool
    /// </summary>
    public enum PlacementHitSurfaceKind
    {
        Ground,
        BlockFace,
    }
}
