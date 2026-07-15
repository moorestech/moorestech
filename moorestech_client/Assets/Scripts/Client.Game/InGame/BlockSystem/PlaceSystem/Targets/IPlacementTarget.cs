using System;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    /// <summary>
    /// 設置対象の多相ターゲット（ブロック/車両/接続ツール/BP/BPコピー）
    /// Polymorphic placement target: block, train car, connect tool, blueprint, or blueprint copy
    /// </summary>
    public interface IPlacementTarget : IEquatable<IPlacementTarget>
    {
    }
}
