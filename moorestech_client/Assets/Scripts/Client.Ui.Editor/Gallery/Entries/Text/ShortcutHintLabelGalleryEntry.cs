using Client.Ui.Components.Text;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Text
{
    /// <summary>
    /// KeyCap・ShortcutHintLabel の見本。keyHintText の行に prefix で置く（Web の KeyControlHint と同じ置き方）
    /// KeyCap and ShortcutHintLabel sample, placed with prefix in a keyHintText row (as Web KeyControlHint does)
    /// </summary>
    public sealed class ShortcutHintLabelGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Text;
        public string ComponentName => "ShortcutHintLabel";

        public VisualElement CreateSample()
        {
            var row = new VisualElement();
            row.AddToClassList(UiTypographyClass.KeyHintText);
            row.AddToClassList("ui-gallery__key-hint-sample");
            // kbd との間は Web の KeyControlHint と同じつまみの値にする（UiGallery.uss）
            // The kbd gap uses the same knob value as Web KeyControlHint (UiGallery.uss)
            var hint = new ShortcutHintLabel("gallery-shortcut-hint", ShortcutHintLayout.Prefix);
            hint.SetHint(new UiText(LocalizationKeys.Ui.PauseMenu.Settings), "Esc");
            row.Add(hint);
            row.Add(new KeyCap("Shift"));
            return row;
        }
    }
}
