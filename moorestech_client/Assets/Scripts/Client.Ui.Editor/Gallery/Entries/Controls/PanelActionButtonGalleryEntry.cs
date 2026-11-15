using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Controls
{
    // PanelActionButtonの見本（通常状態）
    // PanelActionButton sample (normal state)
    public sealed class PanelActionButtonGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Controls;
        public string ComponentName => "PanelActionButton";

        public VisualElement CreateSample()
        {
            var sample = new VisualElement();
            sample.Add(new PanelActionButton("gallery-panel-action-button", new UiText(LocalizationKeys.Ui.PauseMenu.Settings), new ProcessUiOsCursor(new UnityUiOsCursorApplier())));

            return sample;
        }
    }
}
