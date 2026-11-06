using Client.Ui.Components.Panels;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Panels
{
    /// <summary>
    /// PaperSurface の見本。参照 5-challenge-list のノードカードと同じ寸法で 3 行の文を載せ、同じ画面の地の上に置く
    /// PaperSurface sample: three text lines at the size of the node card in reference 5-challenge-list, laid on that screen's backdrop
    /// </summary>
    public sealed class PaperSurfaceGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Panels;
        public string ComponentName => "PaperSurface";

        public VisualElement CreateSample()
        {
            var row = PanelsAndDecorSampleSheet.CreateSampleRow();
            var paper = new PaperSurface("gallery-paper");
            paper.Add(new Label("Second Step"));
            paper.Add(new Label("keep going"));
            paper.Add(new Label("In Progress"));
            paper.AddToClassList("gallery-samples__paper");

            // 面が地と同色のため画面の地に載せる
            // The face matches the gallery backdrop, so lay it on the screen backdrop
            var backdrop = new VisualElement();
            backdrop.Add(paper);
            row.Add(PanelsAndDecorSampleSheet.Cell(backdrop, "gallery-samples__paper-backdrop"));
            return row;
        }
    }
}
