using Client.Ui.Components.Gauges;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Gauges
{
    /// <summary>
    /// BarSeries 見本: 9秒の形・1本・0本
    /// BarSeries sample: nine-second shape, one bar, none
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
