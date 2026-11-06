using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Text
{
    /// <summary>
    /// Typography の見本
    /// Typography sample
    /// </summary>
    public sealed class TypographyGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Text;
        public string ComponentName => "Typography";

        public VisualElement CreateSample()
        {
            var root = new VisualElement();
            var classes = new[]
            {
                UiTypographyClass.TitleOrder1, UiTypographyClass.TitleOrder2, UiTypographyClass.TitleOrder3, UiTypographyClass.TitleOrder4,
                UiTypographyClass.TextMd, UiTypographyClass.TextSm, UiTypographyClass.TextXs,
                UiTypographyClass.TextDimmed, UiTypographyClass.TextWhite, UiTypographyClass.TextDefault,
                UiTypographyClass.TextMuted, UiTypographyClass.TextInsufficient, UiTypographyClass.TextHighContrast,
            };
            foreach (var className in classes)
            {
                var label = new LocalizedLabel { name = $"gallery-{className}" };
                label.AddToClassList(className);
                label.SetText(new UiText(LocalizationKeys.Ui.PauseMenu.Settings));
                root.Add(label);
            }
            return root;
        }
    }
}
