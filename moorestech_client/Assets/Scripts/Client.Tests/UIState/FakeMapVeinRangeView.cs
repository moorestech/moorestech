using System.Collections.Generic;
using Client.Game.InGame.Map.MapVein;

namespace Client.Tests.UIState
{
    /// <summary>
    ///     設置状態から鉱脈範囲表示へ渡るプッシュだけを記録するテスト用の代替実装
    ///     Test double that records only the pushes the placement state sends to the vein range view
    /// </summary>
    public class FakeMapVeinRangeView : IMapVeinRangeView
    {
        public readonly List<bool> PreviewingPushes = new();

        public void ManualUpdate(bool isPlacementPreviewing)
        {
            PreviewingPushes.Add(isPlacementPreviewing);
        }
    }
}
