using Client.Ui.Components.Controls;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Controls
{
    // FormButton の見本（Filled・無効・Danger・Subtle・xl）
    // FormButton samples (Filled, disabled, Danger, Subtle, xl)
    public sealed class FormButtonGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Controls;
        public string ComponentName => "FormButton";

        public VisualElement CreateSample()
        {
            var disabled = new FormButton("gallery-form-button-disabled", new UiText(LocalizationKeys.Ui.BugReport.ResendFailed), FormButtonVariant.Filled);
            disabled.SetEnabled(false);
            var extraLarge = new FormButton("gallery-form-button-xl", new UiText(LocalizationKeys.Ui.BugReport.ResendFailed), FormButtonVariant.Filled);
            extraLarge.SetSize(FormButtonSize.ExtraLarge);

            // 並びの間隔のみ付ける（gap sm 12px）
            // Only the sample spacing (gap sm 12px)
            var sample = new VisualElement();
            sample.style.flexDirection = FlexDirection.Row;
            sample.style.alignItems = Align.Center;
            foreach (var button in new[]
                     {
                         new FormButton("gallery-form-button", new UiText(LocalizationKeys.Ui.BugReport.ResendFailed), FormButtonVariant.Filled),
                         disabled,
                         new FormButton("gallery-form-button-danger", new UiText(LocalizationKeys.Ui.BugReport.ResendFailed), FormButtonVariant.Danger),
                         new FormButton("gallery-form-button-subtle", new UiText(LocalizationKeys.Ui.BugReport.ResendFailed), FormButtonVariant.Subtle),
                         extraLarge,
                     })
            {
                button.style.marginRight = 12;
                sample.Add(button);
            }
            return sample;
        }
    }
}
