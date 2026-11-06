using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// 列数固定のスロットの格子。USS に grid が無いので列数ごとの行に並べ、間隔は USS のつまみで付ける（Web の SlotGrid）
    /// Fixed-column slot grid; USS has no grid, so items go into rows of the column count and the gaps come from USS knobs (the Web SlotGrid)
    /// </summary>
    public sealed class SlotGrid : VisualElement
    {
        private const string CellClass = "slot-grid__cell";
        private const string RowStartClass = "slot-grid__cell--row-start";

        private readonly List<VisualElement> _items = new();
        private readonly VisualElement _tracks;
        private int _columns;
        private VisualElement _lastRow;

        public SlotGrid(string testid, int columns)
        {
            name = testid;
            pickingMode = PickingMode.Ignore;
            AddToClassList("slot-grid");
            _columns = RequirePositive(columns);
            _tracks = new VisualElement { pickingMode = PickingMode.Ignore };
            _tracks.AddToClassList("slot-grid__tracks");
            Add(_tracks);
            RebuildTracks();
        }

        public void SetColumns(int columns)
        {
            _columns = RequirePositive(columns);

            // 列の幅の行と行を作り直し、マスを前から詰め直す
            // Rebuild the track row and the rows, then refill the cells from the front
            ClearRows();
            RebuildTracks();
            for (var i = 0; i < _items.Count; i++) PlaceAt(i);
        }

        public void AddItem(VisualElement item)
        {
            item.AddToClassList(CellClass);
            _items.Add(item);
            PlaceAt(_items.Count - 1);
        }

        public void ClearItems()
        {
            foreach (var item in _items)
            {
                item.RemoveFromClassList(CellClass);
                item.RemoveFromClassList(RowStartClass);
            }
            _items.Clear();
            ClearRows();
        }

        private void ClearRows()
        {
            // 列の幅の行だけ残し、マスの行を外す
            // Keep only the track row and drop the cell rows
            Clear();
            Add(_tracks);
            _lastRow = null;
        }

        private void RebuildTracks()
        {
            // マスが列数より少なくても Web の repeat(cols, --slot-size) と同じ幅を保つ、高さ 0 の列の行
            // A zero-height track row that keeps the Web repeat(cols, --slot-size) width even with fewer cells than columns
            _tracks.Clear();
            for (var i = 0; i < _columns; i++)
            {
                var track = new VisualElement { pickingMode = PickingMode.Ignore };
                track.AddToClassList("slot-grid__track");
                track.EnableInClassList("slot-grid__track--row-start", i == 0);
                _tracks.Add(track);
            }
        }

        private void PlaceAt(int index)
        {
            // 行の頭のマスで新しい行を作る。最初の行だけ上の間隔を持たない
            // A row-start cell opens a new row; only the first row has no top gap
            var column = index % _columns;
            if (column == 0)
            {
                _lastRow = new VisualElement { pickingMode = PickingMode.Ignore };
                _lastRow.AddToClassList("slot-grid__row");
                _lastRow.EnableInClassList("slot-grid__row--first", index == 0);
                Add(_lastRow);
            }
            var item = _items[index];
            item.EnableInClassList(RowStartClass, column == 0);
            _lastRow.Add(item);
        }

        private static int RequirePositive(int columns)
        {
            if (columns < 1) throw new ArgumentOutOfRangeException(nameof(columns), columns, "SlotGrid needs at least one column");
            return columns;
        }
    }
}
