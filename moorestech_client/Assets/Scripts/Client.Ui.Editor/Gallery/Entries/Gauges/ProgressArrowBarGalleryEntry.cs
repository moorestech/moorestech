using Client.Ui.Components.Gauges;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Gauges
{
    /// <summary>
    /// ProgressArrowBar の見本。空・タンクの参照（0.5）・採掘機の参照（0.66）・満杯・NaN を縦に並べる
    /// ProgressArrowBar sample stacking empty, the tank reference (0.5), the miner reference (0.66), full and NaN
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
