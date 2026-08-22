using System.Collections.Generic;

namespace Core.Master
{
    // スポーン距離リング [Inner, Outer)。
    // ・Outerは+infinity可
    // ・帯そのものを持つ（添字で元配列を引き直さない）
    // A spawn-distance ring [Inner, Outer).
    // - Outer may be +infinity
    // - It carries the band itself, so no caller re-indexes the source array
    public readonly struct SpawnDistanceRing<TBand> where TBand : SpawnDistanceBand
    {
        public readonly TBand Band;
        public readonly float Inner;
        public readonly float Outer;

        public SpawnDistanceRing(TBand band, float inner, float outer)
        {
            Band = band;
            Inner = inner;
            Outer = outer;
        }

        public bool Contains(float distance) => Inner <= distance && distance < Outer;

        // 距離範囲[nearest, farthest]とこのリングが重なるか（範囲外のタイルではリング全体を飛ばせる）。
        // Whether this ring overlaps the distance range [nearest, farthest], letting callers skip a ring a tile cannot reach.
        public bool OverlapsDistanceRange(float nearestDistance, float farthestDistance)
            => Inner <= farthestDistance && nearestDistance < Outer;
    }

    // 帯列を昇順リングへ変換する純粋関数と、その規則に反する帯の診断。
    // 生成器とマスタバリデーターが同じ規則を共有するため Core.Master に置く。
    // Pure functions turning bands into ascending rings, plus diagnostics for bands that break those rules.
    // It lives in Core.Master so generators and the master validator share one rule set.
    public static class SpawnDistanceRingPlanner
    {
        public static List<SpawnDistanceRing<TBand>> BuildRings<TBand>(TBand[] bands) where TBand : SpawnDistanceBand
        {
            var rings = new List<SpawnDistanceRing<TBand>>();
            if (bands == null || bands.Length == 0) return rings;

            var radii = new float[bands.Length];
            for (var i = 0; i < bands.Length; i++) radii[i] = bands[i].outerRadiusMeters;

            foreach (var (bandIndex, inner, outer) in PlanRings(radii))
                rings.Add(new SpawnDistanceRing<TBand>(bands[bandIndex], inner, outer));
            return rings;
        }

        // リング化できない外半径列の理由を値で返す（主語は呼び出し側が付ける）。空リストなら妥当。
        // 生成マスタの帯型は SpawnDistanceBand を継承しないため、外半径だけを受け取る。
        // Returns, as values, why a radius list cannot become rings (the caller prefixes the subject); an empty list means valid.
        // It takes only the radii because the generated master band types do not derive from SpawnDistanceBand.
        public static List<string> Diagnose(float[] outerRadiusMeters)
        {
            var problems = new List<string>();
            // 呼び出し元のOuterRadiiOfがnullを未然にNREで弾くため、ここではLength==0だけを見る。
            // Callers' OuterRadiiOf already NREs on null, so only Length==0 is reachable here.
            if (outerRadiusMeters.Length == 0)
            {
                problems.Add("has no spawn-distance bands");
                return problems;
            }

            // -1以外の負値はリングにはなるが無限扱いに紛れる（値の取り違えを見逃さないため個別に見る）。
            // A negative other than -1 still becomes a ring but hides inside the infinite case, so it gets its own check.
            for (var i = 0; i < outerRadiusMeters.Length; i++)
            {
                var outerRadius = outerRadiusMeters[i];
                if (outerRadius < 0f && outerRadius != -1f)
                    problems.Add($"has a negative outer radius ({outerRadius}) other than -1 at bands[{i}]");
            }

            // BuildRingsと同じ計画結果で照合し、リングにならなかった帯を全て問題として返す
            // （NaN・外半径0・外半径重複のいずれも、規則を二重に書かずここで捕まる）。
            // Reconcile against the very plan BuildRings uses and report every band that produced no ring
            // (NaN, a zero outer radius, and duplicates are all caught here without restating the rules).
            var ringedBandIndices = new HashSet<int>();
            foreach (var (bandIndex, _, _) in PlanRings(outerRadiusMeters)) ringedBandIndices.Add(bandIndex);

            for (var i = 0; i < outerRadiusMeters.Length; i++)
            {
                if (ringedBandIndices.Contains(i)) continue;
                problems.Add($"has bands[{i}] whose outer radius ({outerRadiusMeters[i]}) produces no ring (NaN, non-positive, or a duplicate of an earlier band)");
            }
            return problems;
        }

        // 外半径列を昇順リング（帯添字・内半径・外半径）へ計画する唯一の規則。
        // The single rule planning radii into ascending rings (band index, inner, outer).
        private static List<(int BandIndex, float Inner, float Outer)> PlanRings(float[] outerRadiusMeters)
        {
            var planned = new List<(int, float, float)>();

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
                if (inner < key) planned.Add((idx, inner, key));
                inner = key;
            }
            return planned;

            #region Internal

            float ToSortKey(float outerRadius)
                => outerRadius < 0f ? float.PositiveInfinity : outerRadius;

            #endregion
        }
    }
}
