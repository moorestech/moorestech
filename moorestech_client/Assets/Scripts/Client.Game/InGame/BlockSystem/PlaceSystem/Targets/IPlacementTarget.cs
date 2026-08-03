using System;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    /// <summary>
    /// 設置対象の多相ターゲット（ブロック/車両/接続ツール/BP/BPコピー）
    /// Polymorphic placement target: block, train car, connect tool, blueprint, or blueprint copy
    /// </summary>
    public interface IPlacementTarget : IEquatable<IPlacementTarget>
    {
        // 設置対象ID。種別を問わずGuid1本で識別する
        // Placement target id: a single GUID regardless of kind
        Guid Id { get; }
        PlacementTargetKind Kind { get; }

        // 表示名の正はここ1箇所。消費側で種別switchして名前を導出することを禁じる
        // The single source of truth for the display name; consumers must never re-derive it by switching on kind
        string DisplayName { get; }
    }
}
