using System.Collections.Generic;
using MapGenerator.Pipeline.Config;

namespace MapGenerator.Pipeline.Generators
{
    /// <summary>1つの距離リング [Inner, Outer)（Outer は +∞ あり）とそのバンド。</summary>
    public readonly struct OreBandRange
    {
        public readonly OreBand Band;
        public readonly float Inner;
        public readonly float Outer;

        public OreBandRange(OreBand band, float inner, float outer)
        {
            Band = band; Inner = inner; Outer = outer;
        }

        /// <summary>距離 distance がこのリング [Inner, Outer) に入るか（Inner含む・Outer含まず）。</summary>
        public bool Contains(float distance) => distance >= Inner && distance < Outer;
    }

    /// <summary>
    /// OreBand[] を outerRadiusMeters 昇順（-1=無限は末尾・安定ソート）の距離リングに変換する純粋関数。
    /// 副作用（ログ等）を持たない。null 要素はスキップ。縮退（Outer&lt;=Inner：重複半径や2つ目以降の
    /// 無限バンド由来）は除外する。警告は呼び出し側（OrePlacementGenerator）が出す。
    /// </summary>
    public static class OreBandPlanner
    {
        static float SortKey(OreBand b)
            => b.outerRadiusMeters < 0f ? float.PositiveInfinity : b.outerRadiusMeters;

        public static List<OreBandRange> BuildRanges(OreBand[] bands)
        {
            var ranges = new List<OreBandRange>();
            if (bands == null || bands.Length == 0) return ranges;

            var indexed = new List<(OreBand band, int idx)>();
            for (int i = 0; i < bands.Length; i++)
                // null と非有限(NaN)の outerRadiusMeters はスキップ。NaN を残すと SortKey が NaN を返し、
                // inner=NaN 以降の全バンドが Outer>Inner=false で消える（後続の正常バンドの巻き添え）。
                if (bands[i] != null && !float.IsNaN(bands[i].outerRadiusMeters)) indexed.Add((bands[i], i));

            indexed.Sort((a, b) =>
            {
                int c = SortKey(a.band).CompareTo(SortKey(b.band));
                return c != 0 ? c : a.idx.CompareTo(b.idx);
            });

            float inner = 0f;
            foreach (var (band, _) in indexed)
            {
                float outer = SortKey(band);
                if (inner < outer)
                    ranges.Add(new OreBandRange(band, inner, outer));
                inner = outer;
            }
            return ranges;
        }
    }
}
