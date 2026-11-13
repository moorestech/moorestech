using System.Collections.Generic;

namespace Client.Ui.Screens.BlockInventory.Parts.Rows
{
    /// <summary>
    /// 鍵列が同じ間は行を残し中身だけ差し替える（D-37）
    /// Keeps rows while keys match; swaps contents only (D-37)
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

        // 鍵の並びが変わったときだけ行を空にして true を返す（呼び手は true のときだけ作り直す。空にし忘れて鍵が積み増されるのを防ぐ）
        // Empties the rows and returns true only when the key sequence changed (callers rebuild only on true; avoids forgetting to clear and piling up keys)
        public bool ClearIfKeysChanged(IReadOnlyList<TKey> keys)
        {
            if (HasSameKeys(keys)) return false;
            _keys.Clear();
            _rows.Clear();
            return true;
        }

        // 件数と添字ごとの鍵で比べる
        // Compare by count and per-index key
        private bool HasSameKeys(IReadOnlyList<TKey> keys)
        {
            if (keys.Count != _keys.Count) return false;
            var comparer = EqualityComparer<TKey>.Default;
            for (var i = 0; i < keys.Count; i++)
            {
                if (!comparer.Equals(keys[i], _keys[i])) return false;
            }
            return true;
        }

        public void Add(TKey key, TRow row)
        {
            _keys.Add(key);
            _rows.Add(row);
        }
    }
}
