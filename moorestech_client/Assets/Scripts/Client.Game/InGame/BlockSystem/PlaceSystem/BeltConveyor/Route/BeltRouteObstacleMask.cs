using System;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route
{
    /// <summary>
    ///     経路なしの理由を切り分けるために、障害の種類を片方ずつ無視する指定
    ///     Selects which obstacle category to ignore so a missing route can be attributed to terrain or blocks
    /// </summary>
    [Flags]
    public enum BeltRouteObstacleMask
    {
        None = 0,
        IgnoreTerrain = 1,
        IgnoreBlocks = 2,
    }
}
