using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline.Generators
{
    // 1つの距離リング [Inner, Outer)（Outer は +infinity あり）とそのバンド。
    // A single distance ring [Inner, Outer) (Outer may be +infinity) and its band.
    public readonly struct OreBandRange
    {
        public readonly OreBand Band;
        public readonly float Inner;
        public readonly float Outer;

        public OreBandRange(OreBand band, float inner, float outer)
        {
            Band = band; Inner = inner; Outer = outer;
        }

        public bool Contains(float distance) => distance >= Inner && distance < Outer;
    }

    // OreBand[] を outerRadiusMeters 昇順（-1=無限は末尾・安定ソート）の距離リングに変換する純粋関数。
    // Pure function converting OreBand[] into distance rings sorted by outerRadiusMeters (-1=infinite last).
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
