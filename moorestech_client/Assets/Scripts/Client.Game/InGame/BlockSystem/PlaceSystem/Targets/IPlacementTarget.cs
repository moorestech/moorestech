using System;
using System.Collections.Generic;
using Game.PlacementTarget;

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

        // C#側の表示名はここを正とし、C#消費側でkind switchして再導出しない（WebはGuid辞書契約で別解決）
        // This is the C# display-name source of truth; C# consumers must not re-derive it by kind (Web resolves its GUID dictionary contract separately)
        string DisplayName { get; }

        IReadOnlyList<(Guid itemGuid, int count)> CreateRequiredItems();
    }
}
