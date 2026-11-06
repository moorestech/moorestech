using Client.Ui.Components.Controls;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Controls
{
    // IconButton の見本（既定の × の有効・無効）。Web の 3-build-menu.png の × と並べ比べる
    // IconButton samples (default cross, enabled and disabled), compared with the cross in the Web 3-build-menu.png
    public sealed class IconButtonGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Controls;
        public string ComponentName => "IconButton";

        public VisualElement CreateSample()
        {
            var disabled = new IconButton("gallery-icon-button-disabled", new IconButtonCloseGlyph());
            disabled.SetEnabled(false);
            var sample = new VisualElement();
            sample.style.flexDirection = FlexDirection.Row;
            sample.Add(new IconButton("gallery-icon-button", new IconButtonCloseGlyph()));
            sample.Add(disabled);
            return sample;
        }
    }
}
