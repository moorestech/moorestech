using System.Collections.Generic;

namespace Client.Game.InGame.Map.MapVein
{
    /// <summary>
    ///     鉱脈範囲表示が描くべき鉱脈そのものを運ぶ値。表示側に「どの鉱脈が対象か」を導出させない
    ///     Carries the very veins the range view must draw, so the view never derives "which veins qualify" itself
    ///     設置判定と同じ絞り込み結果をプッシュすることで、ボックスは出るのに置けない食い違いを構造的に無くす（ADR 0039）
    ///     Pushing the same filtered set the placement check uses structurally removes "a box is shown but placement is refused" (ADR 0039)
    /// </summary>
    public readonly struct VeinDisplay
    {
        // 描く鉱脈。非表示はnull
        // The veins to draw; null means hidden
        public readonly IReadOnlyList<MapVeinAabb> Veins;

        // チュートリアルの鉱脈限定中だけ強調色にする
        // Only the tutorial's vein restriction draws in the highlight color
        public readonly bool Highlight;

        private VeinDisplay(IReadOnlyList<MapVeinAabb> veins, bool highlight)
        {
            Veins = veins;
            Highlight = highlight;
        }

        public static VeinDisplay Hidden => new(null, false);

        public static VeinDisplay OfVeins(IReadOnlyList<MapVeinAabb> veins, bool highlight)
        {
            return new VeinDisplay(veins, highlight);
        }
    }
}
