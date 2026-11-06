using Client.Ui.State.Text;
using System.Collections.Generic;
using Client.Ui.Components.Controls;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Controls
{
    // ModeSwitchの見本（横2択）
    // ModeSwitch sample (horizontal two options)
    public sealed class ModeSwitchGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Controls;
        public string ComponentName => "ModeSwitch";

        public VisualElement CreateSample()
        {
            var options = new List<ModeSwitchOption<string>>
            {
                new("gallery-mode-switch-option-a", "a", UiText.FromVerbatim("Option A")),
                new("gallery-mode-switch-option-b", "b", UiText.FromVerbatim("Option B")),
            };
            var modeSwitch = new ModeSwitch<string>("gallery-mode-switch", ModeSwitchOrientation.Horizontal, ModeSwitchRole.Group, options);
            modeSwitch.SetSelected("a");
            var sample = new VisualElement();
            sample.Add(modeSwitch);
            return sample;
        }
    }
}
