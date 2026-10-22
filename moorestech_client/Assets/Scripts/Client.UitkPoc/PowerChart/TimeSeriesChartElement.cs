using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.PowerChart
{
    // Factorio電力画面風の多系列折れ線。格子・系列・ホバー縦線を自前で描き、目盛りはLabelで重ねる
    // Factorio-style multi-series line chart: grid, series and hover line drawn by hand, ticks as Labels
    public class TimeSeriesChartElement : VisualElement
    {
        private const float PlotLeft = 46f;
        private const float PlotTop = 8f;
        private const float PlotBottomMargin = 24f;
        private const float PlotRightMargin = 8f;

        private readonly ChartAxisLabels _axisLabels;
        private readonly ChartHoverTooltip _tooltip;
        private readonly string _unit;
        private PowerSeriesSimulator _simulator;
        private Color[] _colors;
        private Vector2[] _pointBuffer;
        private float _yMax = 1f;
        private float _rangeValue = 60f;
        private int _hoverIndex = -1;
        private int _emphasizedSeries = -1;
        private bool _useMesh;

        public TimeSeriesChartElement(string unit)
        {
            _unit = unit;
            AddToClassList("pc-chart");
            _axisLabels = new ChartAxisLabels(this);
            _tooltip = new ChartHoverTooltip();
            Add(_tooltip);
            generateVisualContent += Draw;
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerLeaveEvent>(_ => SetHover(-1, Vector2.zero));
            RegisterCallback<GeometryChangedEvent>(_ => RelayoutLabels());
        }

        public void SetSeries(PowerSeriesSimulator simulator, Color[] colors, string[] names)
        {
            _simulator = simulator;
            _colors = colors;
            _pointBuffer = new Vector2[simulator.PointCount];
            _tooltip.Build(colors, names);
            Refresh();
        }

        public void SetRangeValue(float rangeValue)
        {
            _rangeValue = rangeValue;
            RelayoutLabels();
        }

        public void SetUseMesh(bool useMesh)
        {
            _useMesh = useMesh;
            MarkDirtyRepaint();
        }

        public void SetEmphasizedSeries(int series)
        {
            _emphasizedSeries = series;
            MarkDirtyRepaint();
        }

        // 新しいサンプルが入った後に呼ぶ。上限を切りのよい値へ丸め、目盛りと描画を更新する
        // Call after new samples: round the ceiling, then refresh ticks and drawing
        public void Refresh()
        {
            var yMax = ChartNumberFormat.NiceCeiling(_simulator.MaxVisible() * 1.05f);
            if (!Mathf.Approximately(yMax, _yMax))
            {
                _yMax = yMax;
                RelayoutLabels();
            }
            if (_hoverIndex >= 0) _tooltip.SetValues(_simulator, _hoverIndex, _unit);
            MarkDirtyRepaint();
        }

        private Rect PlotRect()
        {
            return new Rect(PlotLeft, PlotTop, contentRect.width - PlotLeft - PlotRightMargin, contentRect.height - PlotTop - PlotBottomMargin);
        }

        private void RelayoutLabels()
        {
            var plot = PlotRect();
            if (plot.width <= 0) return;
            _axisLabels.Layout(plot.x, plot.y, plot.width, plot.height, _yMax, _unit, _rangeValue);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            var plot = PlotRect();
            var local = (Vector2)evt.localPosition;
            if (_simulator == null || !plot.Contains(local))
            {
                SetHover(-1, local);
                return;
            }
            SetHover(Mathf.RoundToInt((local.x - plot.x) / plot.width * (_simulator.PointCount - 1)), local);
        }

        private void SetHover(int index, Vector2 pointer)
        {
            _hoverIndex = index;
            _tooltip.SetVisible(index >= 0);
            if (index >= 0)
            {
                _tooltip.SetValues(_simulator, index, _unit);
                _tooltip.PlaceNear(pointer, contentRect.size);
            }
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            if (_simulator == null) return;
            var plot = PlotRect();
            if (plot.width <= 0) return;
            var painter = context.painter2D;

            // 格子は1本のパスにまとめて1回で描く
            // Grid lines go into one path stroked once
            painter.lineWidth = 1f;
            painter.strokeColor = new Color(1f, 1f, 1f, 0.07f);
            painter.BeginPath();
            for (var i = 0; i < ChartAxisLabels.HorizontalLines; i++)
            {
                var y = plot.y + plot.height * i / (ChartAxisLabels.HorizontalLines - 1);
                painter.MoveTo(new Vector2(plot.x, y));
                painter.LineTo(new Vector2(plot.xMax, y));
            }
            for (var i = 0; i < ChartAxisLabels.VerticalLines; i++)
            {
                var x = plot.x + plot.width * i / (ChartAxisLabels.VerticalLines - 1);
                painter.MoveTo(new Vector2(x, plot.y));
                painter.LineTo(new Vector2(x, plot.yMax));
            }
            painter.Stroke();

            // 強調中の系列は最後に太く描いて前面へ出す
            // Draw the emphasized series last and thicker so it sits on top
            for (var s = 0; s < _simulator.SeriesCount; s++) if (s != _emphasizedSeries) DrawSeries(s, false);
            if (_emphasizedSeries >= 0) DrawSeries(_emphasizedSeries, true);

            if (_hoverIndex < 0) return;
            var hoverX = plot.x + plot.width * _hoverIndex / (_simulator.PointCount - 1);
            painter.lineWidth = 1f;
            painter.strokeColor = new Color(1f, 1f, 1f, 0.5f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(hoverX, plot.y));
            painter.LineTo(new Vector2(hoverX, plot.yMax));
            painter.Stroke();

            #region Internal

            void DrawSeries(int series, bool emphasized)
            {
                var count = _simulator.PointCount;
                for (var i = 0; i < count; i++)
                {
                    var value = _simulator.Get(series, i);
                    _pointBuffer[i] = new Vector2(plot.x + plot.width * i / (count - 1), plot.yMax - plot.height * Mathf.Clamp01(value / _yMax));
                }
                var color = _colors[series];
                if (_emphasizedSeries >= 0 && !emphasized) color.a = 0.2f;
                var width = emphasized ? 2.5f : 1.4f;
                if (_useMesh)
                {
                    PolylineMeshWriter.Write(context, _pointBuffer, count, width, color);
                    return;
                }
                painter.lineWidth = width;
                painter.lineJoin = LineJoin.Round;
                painter.strokeColor = color;
                painter.BeginPath();
                painter.MoveTo(_pointBuffer[0]);
                for (var i = 1; i < count; i++) painter.LineTo(_pointBuffer[i]);
                painter.Stroke();
            }

            #endregion
        }
    }
}
