using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.UitkPoc.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.PowerChart
{
    // Factorioの電力網情報画面の再現。上段の充足バー、時間幅タブ、3列の折れ線＋系列一覧
    // Factorio electric-network screen: satisfaction bars, range tabs, three columns of chart plus series list
    public class PowerChartDemo : IUitkPocDemo
    {
        private static readonly (string label, float seconds, float axis, float volatility)[] Ranges =
        {
            ("5秒", 5f, 5f, 1f), ("1分", 60f, 60f, 0.9f), ("10分", 600f, 10f, 0.7f), ("1時間", 3600f, 60f, 0.6f),
            ("10時間", 36000f, 10f, 0.45f), ("50時間", 180000f, 50f, 0.35f), ("250時間", 900000f, 250f, 0.3f), ("全期間", 3600000f, 1000f, 0.25f),
        };

        private readonly UitkPocContext _context;
        private readonly List<PowerChartPanel> _panels = new();
        private readonly List<Button> _rangeButtons = new();
        private string[] _iconNames;
        private int _seriesCount = 18;
        private int _pointCount = 300;
        private int _rangeIndex;
        private bool _useMesh;

        public string Title => "電力グラフ";

        public PowerChartDemo(UitkPocContext context)
        {
            _context = context;
        }

        public VisualElement CreateView()
        {
            _iconNames = Directory.GetFiles(Path.Combine(_context.ModDirectory, "assets", "item"), "*.jpeg").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n).ToArray();
            var root = new VisualElement();
            root.styleSheets.Add(Resources.Load<StyleSheet>("UitkPoc/PowerChart"));
            root.AddToClassList("pc-root");

            // 上段: 充足率・発電・蓄電の3本のバー
            // Top row: satisfaction, production and accumulator bars
            var meters = new VisualElement();
            meters.AddToClassList("pc-meters");
            meters.Add(CreateMeter("充足率", 1f, "1.6 GW / 1.6 GW"));
            meters.Add(CreateMeter("発電", 0.21f, "1.6 GW / 7.5 GW"));
            meters.Add(CreateMeter("蓄電", 1f, "28.7 GJ / 28.7 GJ"));
            root.Add(meters);

            var rangeRow = new VisualElement();
            rangeRow.AddToClassList("pc-range-row");
            for (var i = 0; i < Ranges.Length; i++) AddRangeButton(i);
            root.Add(rangeRow);
            root.Add(CreateStressControls());

            var columns = new VisualElement();
            columns.AddToClassList("pc-columns");
            foreach (var (title, unit) in new[] { ("消費", "W"), ("発電", "W"), ("蓄電", "J") })
            {
                var panel = new PowerChartPanel(title, unit);
                _panels.Add(panel);
                columns.Add(panel);
            }
            root.Add(columns);
            RebuildSeries();
            return root;

            #region Internal

            void AddRangeButton(int index)
            {
                var button = new Button(() => SelectRange(index)) { text = Ranges[index].label };
                button.AddToClassList("pc-range");
                _rangeButtons.Add(button);
                rangeRow.Add(button);
            }

            VisualElement CreateMeter(string title, float ratio, string text)
            {
                var meter = new VisualElement();
                meter.AddToClassList("pc-meter");
                meter.Add(new Label(title) { name = "title" });
                var track = new VisualElement();
                track.AddToClassList("pc-meter__track");
                var fill = new VisualElement();
                fill.AddToClassList("pc-meter__fill");
                fill.style.width = Length.Percent(ratio * 100f);
                track.Add(fill);
                var value = new Label(text);
                value.AddToClassList("pc-meter__value");
                track.Add(value);
                meter.Add(track);
                return meter;
            }

            #endregion
        }

        private VisualElement CreateStressControls()
        {
            // 負荷試験用: 系列数・点数・描画方式を変えて計測する
            // Stress controls: vary series count, point count and draw path while measuring
            var row = new VisualElement();
            row.AddToClassList("pc-stress-row");
            var seriesSlider = new SliderInt("系列数", 1, 96) { value = _seriesCount };
            var pointSlider = new SliderInt("点数", 60, 4000) { value = _pointCount };
            var summary = new Label();
            seriesSlider.RegisterValueChangedCallback(evt => { _seriesCount = evt.newValue; RebuildSeries(); UpdateSummary(); });
            pointSlider.RegisterValueChangedCallback(evt => { _pointCount = evt.newValue; RebuildSeries(); UpdateSummary(); });
            var meshToggle = new Toggle("自前メッシュで描く") { value = _useMesh };
            meshToggle.RegisterValueChangedCallback(evt => { _useMesh = evt.newValue; foreach (var panel in _panels) panel.SetUseMesh(_useMesh); });
            row.Add(seriesSlider);
            row.Add(pointSlider);
            row.Add(meshToggle);
            row.Add(summary);
            UpdateSummary();
            return row;

            #region Internal

            void UpdateSummary()
            {
                summary.text = $"消費グラフ {_seriesCount}系列 × {_pointCount}点 = {_seriesCount * _pointCount:N0}頂点/フレーム";
            }

            #endregion
        }

        public void SetStress(int seriesCount, int pointCount, bool useMesh)
        {
            _seriesCount = seriesCount;
            _pointCount = pointCount;
            _useMesh = useMesh;
            RebuildSeries();
            foreach (var panel in _panels) panel.SetUseMesh(_useMesh);
        }

        private void RebuildSeries()
        {
            // 消費は多系列、発電は数系列、蓄電は1系列。値は指数的に減る分布にする
            // Consumption has many series, production a few, accumulator one; values decay exponentially
            var specs = new[] { (count: _seriesCount, top: 4.0e8f, decay: 0.78f), (count: 2, top: 1.1e9f, decay: 0.35f), (count: 1, top: 2.87e10f, decay: 1f) };
            for (var p = 0; p < _panels.Count; p++)
            {
                var (count, top, decay) = specs[p];
                var baseValues = Enumerable.Range(0, count).Select(i => top * Mathf.Pow(decay, i)).ToArray();
                var colors = Enumerable.Range(0, count).Select(i => Color.HSVToRGB((0.6f + i * 0.13f) % 1f, 0.75f, 0.95f)).ToArray();
                var names = Enumerable.Range(0, count).Select(i => _iconNames[(i * 7 + p * 3) % _iconNames.Length]).ToArray();
                var icons = names.Select(_context.LoadItemIcon).ToArray();
                var counts = Enumerable.Range(0, count).Select(i => Mathf.RoundToInt(800f * Mathf.Pow(0.82f, i)) + 1).ToArray();
                _panels[p].SetSeries(new PowerSeriesSimulator(count, _pointCount, baseValues), colors, names, icons, counts);
            }
            SelectRange(_rangeIndex);
        }

        private void SelectRange(int index)
        {
            _rangeIndex = index;
            for (var i = 0; i < _rangeButtons.Count; i++) _rangeButtons[i].EnableInClassList("pc-range--active", i == index);
            var range = Ranges[index];
            foreach (var panel in _panels) panel.SetRange(range.seconds, range.axis, range.volatility);
        }
    }
}
