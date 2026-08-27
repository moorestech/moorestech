using System;
using System.Collections.Generic;
using Client.Game.InGame.Map.MapVein;

namespace Client.Tests.UIState.Fakes
{
    /// <summary>
    ///     設置状態から鉱脈範囲表示へ渡るプッシュだけを記録するテスト用の代替実装
    ///     Test double that records only the pushes the placement state sends to the vein range view
    /// </summary>
    public class FakeMapVeinRangeView : IMapVeinRangeView
    {
        public readonly List<MapVeinKind?> VeinKindPushes = new();
        public readonly List<Guid?> HighlightPushes = new();
        public int ManualUpdateCount;

        public void SetVisibleVeinKind(MapVeinKind? veinKind)
        {
            VeinKindPushes.Add(veinKind);
        }

        public void SetHighlightedVein(Guid? veinGuid)
        {
            HighlightPushes.Add(veinGuid);
        }

        public void ManualUpdate()
        {
            ManualUpdateCount++;
        }
    }
}
