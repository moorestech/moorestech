using Client.Ui.Components.Gauges;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Gauges
{
    /// <summary>
    /// ProgressArrowBar 見本: 空・0.5・0.66・満杯・NaN
    /// ProgressArrowBar sample: empty, 0.5, 0.66, full, NaN
    /// </summary>
    public sealed class ProgressArrowBarGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Gauges;
        public string ComponentName => "ProgressArrowBar";

        public VisualElement CreateSample()
        {
            var root = new VisualElement();
            foreach (var value in new[] { 0f, 0.5f, 0.66f, 1f, float.NaN })
            {
                var bar = new ProgressArrowBar();
                bar.SetValue(value);
                bar.style.marginBottom = 8;
                root.Add(bar);
            }
            return root;
        }
    }
}
