using Client.Ui.Components.Gauges;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Gauges
{
    /// <summary>
    /// BarSeries の見本。ベルトの直近 9 秒の形（古い側の空・途中・満杯・範囲外）と、棒 1 本・0 本を並べる
    /// BarSeries sample: the belt's last nine seconds (empty oldest bars, partial, full, out of range), plus one bar and none
    /// </summary>
    public sealed class BarSeriesGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Gauges;
        public string ComponentName => "BarSeries";

        public VisualElement CreateSample()
        {
            var root = new VisualElement();
            root.Add(Series("gallery-bar-series-belt", new float?[] { null, null, 0.25f, 0.5f, 0.75f, 1f, 1.4f, 0.375f, 0f }));
            root.Add(Series("gallery-bar-series-single", new float?[] { 0.5f }));
            root.Add(Series("gallery-bar-series-empty", new float?[0]));
            return root;

            #region Internal

            VisualElement Series(string testid, float?[] values)
            {
                var series = new BarSeries(testid);
                series.SetValues(values);
                series.style.marginBottom = 8;
                return series;
            }

            #endregion
        }
    }
}
