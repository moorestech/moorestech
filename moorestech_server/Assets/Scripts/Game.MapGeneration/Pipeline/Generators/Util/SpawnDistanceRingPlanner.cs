using System.Collections.Generic;

namespace Game.MapGeneration.Pipeline.Generators.Util
{
    // 1つのスポーン距離リング [Inner, Outer)（Outer は +infinity あり）。BandIndex は元バンド配列の添字。
    // One spawn-distance ring [Inner, Outer) (Outer may be +infinity); BandIndex points back into the source band array.
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

    // 外半径列を outerRadiusMeters 昇順（負値=無限は末尾・安定ソート）のリングへ変換する純粋関数。
    // 鉱脈帯と mapObject 散布帯の両方が使う。バンド型に依存しないよう外半径だけを受け取る。
    // Pure function turning outer radii into rings sorted ascending (negative = infinite last, stable).
    // Shared by vein bands and object-scatter bands; takes only the radii so it stays independent of the band type.
    public static class SpawnDistanceRingPlanner
    {
        public static List<SpawnDistanceRing> BuildRings(float[] outerRadiusMeters)
        {
            var rings = new List<SpawnDistanceRing>();
            if (outerRadiusMeters == null || outerRadiusMeters.Length == 0) return rings;

            // 添字を保持したまま安定ソートする。同じ外半径は元の並び順を保つ。
            // Sort stably while keeping the index; equal radii keep their original order.
            var indexed = new List<(float key, int idx)>();
            for (var i = 0; i < outerRadiusMeters.Length; i++)
                indexed.Add((ToSortKey(outerRadiusMeters[i]), i));
            indexed.Sort((a, b) =>
            {
                var c = a.key.CompareTo(b.key);
                return c != 0 ? c : a.idx.CompareTo(b.idx);
            });

            // 内側から順に [inner, outer) を切る。幅0（重複外半径）はリングにしない。
            // Cut [inner, outer) from the inside out; zero-width rings (duplicate radii) are dropped.
            var inner = 0f;
            foreach (var (key, idx) in indexed)
            {
                if (inner < key) rings.Add(new SpawnDistanceRing(idx, inner, key));
                inner = key;
            }
            return rings;
        }

        static float ToSortKey(float outerRadiusMeters)
            => outerRadiusMeters < 0f ? float.PositiveInfinity : outerRadiusMeters;
    }
}
