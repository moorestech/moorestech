using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.PowerChart
{
    // 系列ごとの「アイコン＋台数・色つきバー・現在値」行を2列で並べる。行ホバーでグラフの系列を強調する
    // Two-column rows of icon with count, colored bar and current value; hovering a row emphasizes the series
    public class SeriesBarList : ScrollView
    {
        private readonly List<(VisualElement fill, Label value)> _rows = new();
        private readonly string _unit;
        private TimeSeriesChartElement _chart;

        public SeriesBarList(string unit)
        {
            _unit = unit;
            AddToClassList("pc-list");
            contentContainer.AddToClassList("pc-list__content");
        }

        public void Build(TimeSeriesChartElement chart, Color[] colors, Texture2D[] icons, int[] counts)
        {
            _chart = chart;
            Clear();
            _rows.Clear();
            for (var s = 0; s < colors.Length; s++) AddRow(s);

            #region Internal

            void AddRow(int series)
            {
                var row = new VisualElement();
                row.AddToClassList("pc-row");
                var icon = new VisualElement();
                icon.AddToClassList("pc-row__icon");
                if (icons[series] != null) icon.style.backgroundImage = icons[series];
                var count = new Label(counts[series] >= 1000 ? $"{counts[series] / 1000f:0.#}k" : counts[series].ToString());
                count.AddToClassList("pc-row__count");
                icon.Add(count);
                var track = new VisualElement();
                track.AddToClassList("pc-row__track");
                var fill = new VisualElement();
                fill.AddToClassList("pc-row__fill");
                fill.style.backgroundColor = colors[series];
                track.Add(fill);
                var value = new Label();
                value.AddToClassList("pc-row__value");
                row.Add(icon);
                row.Add(track);
                row.Add(value);
                row.RegisterCallback<PointerEnterEvent>(_ => _chart.SetEmphasizedSeries(series));
                row.RegisterCallback<PointerLeaveEvent>(_ => _chart.SetEmphasizedSeries(-1));
                Add(row);
                _rows.Add((fill, value));
            }

            #endregion
        }

        // 最新値でバー幅と数値を更新する。幅はUSSのtransitionで滑らかに追従する
        // Update bar widths and numbers from the latest values; USS transitions smooth the width
        public void Refresh(PowerSeriesSimulator simulator)
        {
            var max = 1f;
            for (var s = 0; s < simulator.SeriesCount; s++) max = Mathf.Max(max, simulator.Latest(s));
            for (var s = 0; s < _rows.Count; s++)
            {
                var latest = simulator.Latest(s);
                _rows[s].fill.style.width = Length.Percent(latest / max * 100f);
                _rows[s].value.text = ChartNumberFormat.Si(latest, _unit);
            }
        }
    }
}
