using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.PowerChart
{
    // 見出し・折れ線・系列一覧を縦に積んだ1列分のパネル。サンプルの進行もここで持つ
    // One column panel stacking header, chart and series list; also owns sample stepping
    public class PowerChartPanel : VisualElement
    {
        private readonly TimeSeriesChartElement _chart;
        private readonly SeriesBarList _list;
        private PowerSeriesSimulator _simulator;
        private float _sampleInterval = 1f / 60f;
        private float _volatility = 1f;
        private float _sampleTimer;
        private float _listTimer;

        public PowerChartPanel(string title, string unit)
        {
            AddToClassList("pc-panel");
            var header = new VisualElement();
            header.AddToClassList("pc-panel__header");
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("pc-panel__title");
            header.Add(titleLabel);
            header.Add(new DropdownField(new System.Collections.Generic.List<string> { "すべて（合算）", "上位10" }, 0));
            var reset = new Button { text = "↺" };
            reset.AddToClassList("pc-panel__reset");
            header.Add(reset);
            Add(header);
            _chart = new TimeSeriesChartElement(unit);
            Add(_chart);
            _list = new SeriesBarList(unit);
            Add(_list);
            schedule.Execute(timer => Tick(timer.deltaTime / 1000f)).Every(0);
        }

        public void SetSeries(PowerSeriesSimulator simulator, Color[] colors, string[] names, Texture2D[] icons, int[] counts)
        {
            _simulator = simulator;
            _chart.SetSeries(simulator, colors, names);
            _list.Build(_chart, colors, icons, counts);
            _list.Refresh(simulator);
        }

        // 時間幅を変えたら、1サンプルの間隔と揺らぎの大きさを変えて履歴を作り直す
        // Changing the range resets sample interval and volatility, then regenerates history
        public void SetRange(float rangeSeconds, float axisValue, float volatility)
        {
            _sampleInterval = rangeSeconds / _simulator.PointCount;
            _volatility = volatility;
            for (var i = 0; i < _simulator.PointCount; i++) _simulator.Step(_volatility);
            _chart.SetRangeValue(axisValue);
            _chart.Refresh();
        }

        public void SetUseMesh(bool useMesh)
        {
            _chart.SetUseMesh(useMesh);
        }

        private void Tick(float deltaSeconds)
        {
            if (_simulator == null) return;
            _sampleTimer += deltaSeconds;
            var stepped = false;
            while (_sampleTimer >= _sampleInterval)
            {
                _sampleTimer -= _sampleInterval;
                _simulator.Step(_volatility);
                stepped = true;
            }
            if (stepped) _chart.Refresh();

            // 一覧は0.25秒ごとに更新して文字の書き換えを抑える
            // Refresh the list every 0.25s to limit text rebuilds
            _listTimer += deltaSeconds;
            if (_listTimer < 0.25f) return;
            _listTimer = 0f;
            _list.Refresh(_simulator);
        }
    }
}
