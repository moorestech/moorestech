using Client.Ui.Components.Controls;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Controls
{
    // PanelActionButton の見本（通常の状態。ホバー・押下は C2 の部品レーンが足す）
    // PanelActionButton sample (normal state; hover and pressed are added by the C2 component lanes)
    public sealed class PanelActionButtonGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Controls;
        public string ComponentName => "PanelActionButton";

        public VisualElement CreateSample()
        {
            var sample = new VisualElement();
            sample.Add(new PanelActionButton("gallery-panel-action-button", LocalizationKeys.Ui.PauseMenu.Settings));
            return sample;
        }
    }
}
