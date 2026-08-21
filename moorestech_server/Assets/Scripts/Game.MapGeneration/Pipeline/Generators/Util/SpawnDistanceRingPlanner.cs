using System.Collections.Generic;

namespace Game.MapGeneration.Pipeline.Generators.Util
{
    // スポーン距離リング [Inner, Outer)。
    // ・Outerは+infinity可
    // ・BandIndexは元配列の添字
    // A spawn-distance ring [Inner, Outer).
    // - Outer may be +infinity
    // - BandIndex points back into the source array
    public readonly struct SpawnDistanceRing
    {
        public readonly int BandIndex;
        public readonly float Inner;
        public readonly float Outer;

        public SpawnDistanceRing(int bandIndex, float inner, float outer)
        {
            BandIndex = bandIndex;
            Inner = inner;
            Outer = outer;
        }

        public bool Contains(float distance) => Inner <= distance && distance < Outer;
    }

    // 外半径列を昇順リングへ変換する純粋関数。
    // 鉱脈帯と mapObject 散布帯の両方が使う。バンド型に依存しないよう外半径だけを受け取る。
    // Pure function turning outer radii into ascending rings.
    // Shared by vein bands and object-scatter bands; takes only the radii so it stays independent of the band type.
    public static class SpawnDistanceRingPlanner
    {
        public static List<SpawnDistanceRing> BuildRings(float[] outerRadiusMeters)
        {
            var rings = new List<SpawnDistanceRing>();
            if (outerRadiusMeters == null || outerRadiusMeters.Length == 0) return rings;

            // 添字保持のまま安定ソート。
            // Sort stably while keeping the index.
            var indexed = new List<(float key, int idx)>();
            for (var i = 0; i < outerRadiusMeters.Length; i++)
            {
                // NaN混入バンドは汚染源になるため個別にスキップする（他バンドの生成を道連れにしない）。
                // Skip a NaN band individually so it doesn't poison the sort key for every other band.
                if (float.IsNaN(outerRadiusMeters[i])) continue;
                indexed.Add((ToSortKey(outerRadiusMeters[i]), i));
            }
            indexed.Sort((a, b) =>
            {
                var c = a.key.CompareTo(b.key);
                return c != 0 ? c : a.idx.CompareTo(b.idx);
            });

            // 内側から[inner,outer)を切る。
            // Cut [inner, outer) from the inside out.
            var inner = 0f;
            foreach (var (key, idx) in indexed)
            {
                if (inner < key) rings.Add(new SpawnDistanceRing(idx, inner, key));
                inner = key;
            }
            return rings;

            #region Internal

            float ToSortKey(float outerRadius)
                => outerRadius < 0f ? float.PositiveInfinity : outerRadius;

            #endregion
        }
    }
}
