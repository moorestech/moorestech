using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search
{
    /// <summary>
    ///     f値ごとの待ち行列。コストが整数なので優先度付きキューの代わりにf値の添字で並べる（ParamegaFactoryのDjkと同じ方式）
    ///     Queues keyed by f value; with integer costs they replace a priority queue (the same scheme as ParamegaFactory's Djk)
    /// </summary>
    public sealed class BeltRouteCostBuckets
    {
        // f値の上限＝探索できる経路コストの上限。超える経路は無しとみなす
        // The f cap is the cap on route cost; routes beyond it are treated as absent
        public const int Count = 8192;

        private readonly List<(int g, int state)>[] _buckets = new List<(int, int)>[Count];

        public BeltRouteCostBuckets()
        {
            for (var i = 0; i < Count; i++) _buckets[i] = new List<(int, int)>();
        }

        public List<(int g, int state)> At(int f) => _buckets[f];

        // 上限以上のf値は積めずfalse
        // An f at or beyond the cap cannot be queued and yields false
        public bool TryAdd(int f, int g, int state)
        {
            if (Count <= f) return false;
            _buckets[f].Add((g, state));
            return true;
        }

        // from以上で中身のある最初のf値。無ければCount
        // The first non-empty f at or after from; Count when none
        public int FirstActive(int from)
        {
            for (var f = from; f < Count; f++)
            {
                if (0 < _buckets[f].Count) return f;
            }
            return Count;
        }

        public void Clear()
        {
            foreach (var bucket in _buckets) bucket.Clear();
        }
    }
}
