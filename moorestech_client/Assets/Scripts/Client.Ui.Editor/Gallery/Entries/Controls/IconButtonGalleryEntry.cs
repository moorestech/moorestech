using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input.OsCursor;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Controls
{
    // IconButton の見本（既定の × の有効・無効）
    // IconButton samples (default cross, enabled and disabled)
    public sealed class IconButtonGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Controls;
        public string ComponentName => "IconButton";

        public VisualElement CreateSample()
        {
            // 指カーソルは本物の当て先で出す（前例 NodeGraphViewGalleryEntry）
            // The pointer cursor uses the real applier (precedent NodeGraphViewGalleryEntry)
            var osCursor = new ProcessUiOsCursor(new UnityUiOsCursorApplier());
            var disabled = new IconButton("gallery-icon-button-disabled", new IconButtonCloseGlyph(), osCursor);
            disabled.SetEnabled(false);
            var sample = new VisualElement();
            sample.style.flexDirection = FlexDirection.Row;
            sample.Add(new IconButton("gallery-icon-button", new IconButtonCloseGlyph(), osCursor));

            sample.Add(disabled);
            return sample;
        }
    }
}
