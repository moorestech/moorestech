using Client.Ui.Components.Controls;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Panels
{
    /// <summary>
    /// DialogFrame の見本（幕の色の地に置く）
    /// DialogFrame sample (placed on the scrim color)
    /// </summary>
    public sealed class DialogFrameGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Panels;
        public string ComponentName => "DialogFrame";

        public VisualElement CreateSample()
        {
            var row = PanelsAndDecorSampleSheet.CreateSampleRow();
            var frame = new DialogFrame("gallery-dialog-frame");
            frame.SetTitle(UiText.FromVerbatim("Blueprint name"));

            // Web ModalHost の説明文（Text size="sm" c="dimmed" mb="lg"）。下の間は置く側の USS が持つ
            // The Web ModalHost description (Text size="sm" c="dimmed" mb="lg"); the placer's USS owns the gap below
            var message = new LocalizedLabel { name = "gallery-dialog-frame-message" };
            message.AddToClassList(UiTypographyClass.TextSm);
            message.AddToClassList(UiTypographyClass.TextDimmed);
            message.AddToClassList("gallery-samples__dialog-message");
            message.SetText(UiText.FromVerbatim("Enter a name for the blueprint"));
            frame.Add(message);
            var input = new GameTextField("gallery-dialog-frame-input", false, GameTextFieldLook.MantineInput);
            input.AddToClassList("gallery-samples__dialog-input");
            frame.Add(input);
            frame.AddAction(new FormButton("gallery-dialog-frame-save", UiText.FromVerbatim("Save"), FormButtonVariant.Filled));

            // 枠は幕の上に出るので、ギャラリーでも幕の色の地に載せて比べる
            // The frame appears over the backdrop, so compare it on a backdrop-colored ground in the gallery too
            var backdrop = new VisualElement();
            backdrop.Add(frame);
            row.Add(PanelsAndDecorSampleSheet.Cell(backdrop, "gallery-samples__modal-backdrop"));
            return row;
        }
    }
}
