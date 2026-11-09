using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Gauges
{
    /// <summary>
    /// 0〜1 の値を古い順に縦棒で並べる読み取り専用の時系列（Web の shared/ui/BarSeries）。null の棒は溝だけ
    /// Read-only time series of zero-to-one values as vertical bars, oldest first (Web shared/ui/BarSeries); a null bar draws only its track
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

            // null は溝だけなので 0、値は NaN と範囲外を 0..1 へ丸める（Web の index.tsx の式）
            // Null draws only the track so it is 0; values clamp NaN and out-of-range to 0..1 (the Web index.tsx expression)
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
