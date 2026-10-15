namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Modes
{
    /// <summary>
    ///     ゴーストを出したか、出さなかった理由。既定値は照準面なし
    ///     Whether a ghost is shown, or why not; the default is no aim surface
    /// </summary>
    public enum GearChainPoleGhostState
    {
        NoAimSurface,
        TooFar,
        GroundNotFound,
        Shown,
    }
}
