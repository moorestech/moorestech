using System.Collections.Generic;
using Client.Ui.Components.Controls;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Controls
{
    // ModeSwitch の見本（横 2 択で片方が選択。縦・無効は C3-b が足す）
    // ModeSwitch sample (horizontal two options with one selected; vertical and disabled are added by C3-b)
    public sealed class ModeSwitchGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Controls;
        public string ComponentName => "ModeSwitch";

        public VisualElement CreateSample()
        {
            var options = new List<ModeSwitchOption<string>>
            {
                new("gallery-mode-switch-option-a", "a", null, "Option A"),
                new("gallery-mode-switch-option-b", "b", null, "Option B"),
            };
            var modeSwitch = new ModeSwitch<string>("gallery-mode-switch", ModeSwitchOrientation.Horizontal, options);
            modeSwitch.SetSelected("a");
            var sample = new VisualElement();
            sample.Add(modeSwitch);
            return sample;
        }
    }
}
