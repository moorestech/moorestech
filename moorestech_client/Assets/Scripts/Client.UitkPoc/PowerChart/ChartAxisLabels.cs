using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Client.UitkPoc.PowerChart
{
    // 縦軸（SI表記）と横軸（経過時間）の目盛りラベル
    // Tick labels for the vertical axis (SI) and horizontal axis (elapsed time)
    public class ChartAxisLabels
    {
        public const int HorizontalLines = 7;
        public const int VerticalLines = 21;

        private readonly List<Label> _yLabels = new();
        private readonly List<Label> _xLabels = new();

        public ChartAxisLabels(VisualElement owner)
        {
            for (var i = 0; i < HorizontalLines; i++) _yLabels.Add(Create(owner, "pc-axis-label--y"));
            for (var i = 0; i < VerticalLines; i++) _xLabels.Add(Create(owner, "pc-axis-label--x"));

            #region Internal

            Label Create(VisualElement parent, string modifier)
            {
                var label = new Label();
                label.AddToClassList("pc-axis-label");
                label.AddToClassList(modifier);
                label.pickingMode = PickingMode.Ignore;
                parent.Add(label);
                return label;
            }

            #endregion
        }

        // プロット領域の矩形に合わせてラベルを並べ直す
        // Re-place labels to match the plot rectangle
        public void Layout(float plotLeft, float plotTop, float plotWidth, float plotHeight, float yMax, string unit, float rangeValue)
        {
            for (var i = 0; i < HorizontalLines; i++)
            {
                var ratio = i / (float)(HorizontalLines - 1);
                _yLabels[i].text = ChartNumberFormat.Si(yMax * (1f - ratio), unit).Replace(" ", "");
                _yLabels[i].style.top = plotTop + plotHeight * ratio - 7f;
                _yLabels[i].style.width = plotLeft - 4f;
            }
            for (var i = 0; i < VerticalLines; i++)
            {
                var ratio = i / (float)(VerticalLines - 1);
                var value = rangeValue * (1f - ratio);
                _xLabels[i].text = value >= 10f ? value.ToString("0") : value.ToString("0.#");
                _xLabels[i].style.left = plotLeft + plotWidth * ratio - 12f;
                _xLabels[i].style.top = plotTop + plotHeight + 2f;
            }
        }
    }
}
