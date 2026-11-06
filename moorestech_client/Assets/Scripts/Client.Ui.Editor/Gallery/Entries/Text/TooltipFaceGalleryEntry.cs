using Client.Ui.Components.Text;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Text
{
    /// <summary>
    /// TooltipFace の見本。Cursor の 2 行と、Hover の折り返す長文
    /// TooltipFace sample: a two-line Cursor face and a wrapping long Hover face
    /// </summary>
    public sealed class TooltipFaceGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Text;
        public string ComponentName => "TooltipFace";

        public VisualElement CreateSample()
        {
            // 列の既定の stretch だと Cursor が下の Hover の幅まで伸びるので、各面を自分の幅にする
            // The column's default stretch would widen Cursor to the Hover below, so keep each face at its own width
            var root = new VisualElement();
            root.style.alignItems = Align.FlexStart;
            var cursor = new TooltipFace("gallery-cursor-tooltip", TooltipFaceLook.Cursor);
            cursor.SetLines(new[] { new UiText(LocalizationKeys.Ui.PauseMenu.Settings), new UiText(LocalizationKeys.Ui.PauseMenu.Back) });
            cursor.style.marginBottom = 16;
            root.Add(cursor);
            var hover = new TooltipFace("gallery-hover-tooltip", TooltipFaceLook.Hover);
            hover.SetText(new UiText(LocalizationKeys.Ui.Settings.GraphicsRecovered));
            root.Add(hover);
            return root;
        }
    }
}
