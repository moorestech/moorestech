using System.Collections.Generic;

namespace Core.Master
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

    // 外半径列を昇順リングへ変換する純粋関数と、その規則に反する帯の診断。
    // 生成器とマスタバリデーターが同じ規則を共有するため Core.Master に置く（バンド型に依存しないよう外半径だけを受け取る）。
    // Pure functions turning outer radii into ascending rings, plus diagnostics for radii that break those rules.
    // It lives in Core.Master so generators and the master validator share one rule set; it takes only radii, staying independent of the band type.
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

        // リング化できない外半径列の理由を値で返す（主語は呼び出し側が付ける）。空リストなら妥当。
        // Returns, as values, why a radius list cannot become rings (the caller prefixes the subject); an empty list means valid.
        public static List<string> Diagnose(float[] outerRadiusMeters)
        {
            var problems = new List<string>();
            if (outerRadiusMeters == null || outerRadiusMeters.Length == 0)
            {
                problems.Add("has no spawn-distance bands");
                return problems;
            }

            // -1以外の負値は無限扱いに紛れ、重複外半径は後者がリングにならず黙って消える。
            // A negative other than -1 hides inside the infinite case, and a duplicate outer radius silently drops the later band.
            var seenKeys = new HashSet<float>();
            foreach (var outerRadius in outerRadiusMeters)
            {
                if (outerRadius < 0f && outerRadius != -1f)
                    problems.Add($"has a negative outer radius ({outerRadius}) other than -1");

                var key = outerRadius < 0f ? float.PositiveInfinity : outerRadius;
                if (!seenKeys.Add(key))
                    problems.Add($"has bands with a duplicate outer radius ({outerRadius})");
            }
            return problems;
        }
    }
}
