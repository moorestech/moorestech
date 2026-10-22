using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.PowerChart
{
    // ホバー位置の各系列の値を大きい順に並べるツールチップ。画面端では反対側へ反転する
    // Tooltip listing series values at the hover point, largest first; flips side near edges
    public class ChartHoverTooltip : VisualElement
    {
        private const int MaxRows = 8;

        private readonly List<(VisualElement swatch, Label name, Label value)> _rows = new();
        private readonly Label _header;
        private Color[] _colors;
        private string[] _names;

        public ChartHoverTooltip()
        {
            AddToClassList("pc-tooltip");
            pickingMode = PickingMode.Ignore;
            _header = new Label();
            _header.AddToClassList("pc-tooltip__header");
            Add(_header);
            SetVisible(false);
        }

        public void Build(Color[] colors, string[] names)
        {
            _colors = colors;
            _names = names;
            foreach (var row in _rows) row.swatch.parent.RemoveFromHierarchy();
            _rows.Clear();
            for (var i = 0; i < Mathf.Min(MaxRows, colors.Length); i++)
            {
                var row = new VisualElement();
                row.AddToClassList("pc-tooltip__row");
                var swatch = new VisualElement();
                swatch.AddToClassList("pc-tooltip__swatch");
                var name = new Label();
                name.AddToClassList("pc-tooltip__name");
                var value = new Label();
                value.AddToClassList("pc-tooltip__value");
                row.Add(swatch);
                row.Add(name);
                row.Add(value);
                Add(row);
                _rows.Add((swatch, name, value));
            }
        }

        public void SetVisible(bool visible)
        {
            style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetValues(PowerSeriesSimulator simulator, int index, string unit)
        {
            // 値の大きい順に上位だけを行へ割り当てる
            // Assign only the top values, largest first, to the rows
            var total = 0f;
            for (var s = 0; s < simulator.SeriesCount; s++) total += simulator.Get(s, index);
            _header.text = $"合計 {ChartNumberFormat.Si(total, unit)}";
            var ordered = Enumerable.Range(0, simulator.SeriesCount).OrderByDescending(s => simulator.Get(s, index)).Take(_rows.Count).ToList();
            for (var r = 0; r < _rows.Count; r++)
            {
                var series = ordered[r];
                _rows[r].swatch.style.backgroundColor = _colors[series];
                _rows[r].name.text = _names[series];
                _rows[r].value.text = ChartNumberFormat.Si(simulator.Get(series, index), unit);
            }
        }

        public void PlaceNear(Vector2 pointer, Vector2 containerSize)
        {
            // 右下に置けなければ左／上へ反転させる（floating-uiのflip相当）
            // Flip to the left/top when bottom-right does not fit (like floating-ui flip)
            var size = new Vector2(resolvedStyle.width, resolvedStyle.height);
            if (float.IsNaN(size.x) || size.x <= 0) size = new Vector2(200f, 160f);
            var x = pointer.x + 14f + size.x > containerSize.x ? pointer.x - 14f - size.x : pointer.x + 14f;
            var y = pointer.y + 14f + size.y > containerSize.y ? pointer.y - 14f - size.y : pointer.y + 14f;
            style.left = Mathf.Max(0f, x);
            style.top = Mathf.Max(0f, y);
        }
    }
}
