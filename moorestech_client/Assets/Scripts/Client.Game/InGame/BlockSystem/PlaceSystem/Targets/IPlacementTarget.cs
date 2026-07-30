using System;
using Game.PlacementTarget;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    /// <summary>
    /// 設置対象の多相ターゲット（ブロック/車両/接続ツール/BP/ビルドツール）
    /// Polymorphic placement target: block, train car, connect tool, blueprint, or build tool
    /// </summary>
    public interface IPlacementTarget : IEquatable<IPlacementTarget>
    {
        // 設置対象ID。種別を問わずGuid1本で識別する
        // Placement target id: a single GUID regardless of kind
        Guid Id { get; }
        PlacementTargetKind Kind { get; }
    }
}
