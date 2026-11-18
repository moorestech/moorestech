using Client.Ui.Components.Gauges;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Gauges
{
    /// <summary>
    /// ProgressArrowGlyph 見本: 静止・0・0.42・0.5・満杯
    /// ProgressArrowGlyph sample: static, 0, 0.42, 0.5, full
    /// </summary>
    public sealed class ProgressArrowGlyphGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Gauges;
        public string ComponentName => "ProgressArrowGlyph";

        public VisualElement CreateSample()
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.Add(new ProgressArrowGlyph("gallery-progress-arrow-idle"));
            foreach (var value in new[] { 0f, 0.42f, 0.5f, 1f })
            {
                var glyph = new ProgressArrowGlyph($"gallery-progress-arrow-{value}");
                glyph.SetProgress(value);
                glyph.style.marginLeft = 12;
                root.Add(glyph);
            }
            return root;
        }
    }
}
