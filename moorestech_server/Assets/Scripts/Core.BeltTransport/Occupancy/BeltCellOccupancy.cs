using System.Collections.Generic;

namespace Core.BeltTransport
{
    internal sealed class BeltCellOccupancy
    {
        private readonly Dictionary<int, int> counts = new Dictionary<int, int>();
        private readonly SortedDictionary<int, int> changes = new SortedDictionary<int, int>();

        internal void Add(int cellId, int amount)
        {
            // 同じ公開境界内の出入りを相殺する。
            // Cancel arrivals and departures within the same publication boundary.
            int count = counts.TryGetValue(cellId, out var previous) ? previous + amount : amount;
            if (count == 0) counts.Remove(cellId);
            else counts[cellId] = count;
            Record(cellId, amount);
        }

        internal void ReleasePaths()
        {
            // 再構築時だけ旧所有数を解放し、復元の加算とまとめる。
            // Release old ownership only during rebuilding and combine it with restored arrivals.
            foreach (var pair in counts) Record(pair.Key, -pair.Value);
            counts.Clear();
        }

        internal IReadOnlyDictionary<int, int> DrainChanges()
        {
            var result = new SortedDictionary<int, int>(changes);
            changes.Clear();
            return result;
        }

        private void Record(int cellId, int amount)
        {
            int change = changes.TryGetValue(cellId, out var previous) ? previous + amount : amount;
            if (change == 0) changes.Remove(cellId);
            else changes[cellId] = change;
        }
    }
}
