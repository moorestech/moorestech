using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Gauges
{
    /// <summary>
    /// 古い順の縦棒の時系列（Web の BarSeries）。null は溝だけ
    /// Oldest-first vertical-bar time series (Web BarSeries); null draws only the track
    /// </summary>
    public sealed class BarSeries : VisualElement
    {
        private const string BarClassName = "bar-series__bar";
        private const string LeadingBarClassName = "bar-series__bar--leading";
        private const string EmptyBarClassName = "bar-series__bar--empty";

        private readonly List<VisualElement> _bars = new();
        private readonly List<VisualElement> _fills = new();

        public BarSeries(string testid)
        {
            name = testid;
            pickingMode = PickingMode.Ignore;
            AddToClassList("bar-series");
        }

        public void SetValues(IReadOnlyList<float?> ratios01)
        {
            // 棒の数を値の数にそろえてから、古い順に左から高さを当てる（Web は values.map で毎回作り直す）
            // Match the bar count to the values, then set heights left to right, oldest first (the Web rebuilds through values.map)
            while (_bars.Count < ratios01.Count) AddBar();
            while (ratios01.Count < _bars.Count) RemoveLastBar();
            for (var i = 0; i < ratios01.Count; i++)
            {
                var ratio = ratios01[i];
                _bars[i].EnableInClassList(EmptyBarClassName, !ratio.HasValue);
                _fills[i].style.height = Length.Percent(ClampedRatio(ratio) * 100f);
            }

            #region Internal

            void AddBar()
            {
                // 先頭の棒だけ隙間を持たない（USS に gap が無いので左の margin で写す）
                // Only the leading bar has no gap (USS lacks gap, so the left margin carries it)
                var bar = NewPart(BarClassName);
                bar.EnableInClassList(LeadingBarClassName, _bars.Count == 0);

                // 線は充填より先に置き、充填が内側の線を覆う Web の重なりにそろえる
                // The outline goes before the fill so the fill covers the inset line, matching the Web layering
                bar.Add(NewPart("bar-series__bar-outline"));
                var fill = NewPart("bar-series__bar-fill");
                bar.Add(fill);
                Add(bar);
                _bars.Add(bar);
                _fills.Add(fill);
            }

            void RemoveLastBar()
            {
                var last = _bars.Count - 1;
                _bars[last].RemoveFromHierarchy();
                _bars.RemoveAt(last);
                _fills.RemoveAt(last);
            }

            // null は 0、NaN・範囲外は 0..1 へ丸める
            // Null is 0; NaN and out-of-range clamp to 0..1
            float ClampedRatio(float? ratio)
            {
                return ratio.HasValue ? GaugeRatio.Clamp01(ratio.Value) : 0f;
            }

            VisualElement NewPart(string className)
            {
                var part = new VisualElement { pickingMode = PickingMode.Ignore };
                part.AddToClassList(className);
                return part;
            }

            #endregion
        }
    }
}
