using Client.Ui.Components.Gauges;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Gauges
{
    /// <summary>
    /// GaugeBar の見本。空・途中・満杯・NaN を幅 240px で並べる
    /// GaugeBar sample with empty, partial, full and NaN at 240px wide
    /// </summary>
    public sealed class GaugeBarGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Gauges;
        public string ComponentName => "GaugeBar";

        public VisualElement CreateSample()
        {
            var root = new VisualElement();
            foreach (var value in new[] { 0f, 0.375f, 1f, float.NaN })
            {
                var gauge = new GaugeBar($"gallery-gauge-{value}");
                gauge.SetValue(value);
                gauge.style.width = 240;
                gauge.style.marginBottom = 8;
                root.Add(gauge);
            }
            return root;
        }
    }
}
