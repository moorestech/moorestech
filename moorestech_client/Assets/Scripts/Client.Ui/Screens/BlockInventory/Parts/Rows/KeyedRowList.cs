using System.Collections.Generic;

namespace Client.Ui.Screens.BlockInventory.Parts.Rows
{
    /// <summary>
    /// 鍵の列が同じ間は行（枠）を残して中身だけ差し替える並びの比較 1 本（採掘機・ポンプの毎分の行、ブロックの枠の並びが共有する。D-37）
    /// The single keyed-list comparison that keeps rows (slots) while the key sequence is unchanged and only swaps contents (shared by miner/pump per-minute rows and block slot lists; D-37)
    /// </summary>
    public sealed class KeyedRowList<TKey, TRow>
    {
        private readonly List<TKey> _keys = new();
        private readonly List<TRow> _rows = new();

        public int Count => _rows.Count;

        public TKey KeyAt(int index)
        {
            return _keys[index];
        }

        public TRow RowAt(int index)
        {
            return _rows[index];
        }

        // 件数と添字ごとの鍵の一致で比べる（同じ鍵が並んでも添字で見る）
        // Compare by count and per-index key equality (duplicate keys are told apart by index)
        public bool HasSameKeys(IReadOnlyList<TKey> keys)
        {
            if (keys.Count != _keys.Count) return false;
            var comparer = EqualityComparer<TKey>.Default;
            for (var i = 0; i < keys.Count; i++)
            {
                if (!comparer.Equals(keys[i], _keys[i])) return false;
            }
            return true;
        }

        public void Clear()
        {
            _keys.Clear();
            _rows.Clear();
        }

        public void Add(TKey key, TRow row)
        {
            _keys.Add(key);
            _rows.Add(row);
        }
    }
}
