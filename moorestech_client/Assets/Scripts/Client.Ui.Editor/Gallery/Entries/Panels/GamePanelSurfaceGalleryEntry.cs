using Client.Ui.Components.Decor;
using Client.Ui.Components.Panels;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Panels
{
    /// <summary>
    /// GamePanelSurface の見本。hud（目標 1 行）と、1280×204 の舞台の下端に敷いた skit の帯を並べる
    /// GamePanelSurface samples: a hud with one objective line and a skit band laid at the bottom of a 1280x204 stage
    /// </summary>
    public sealed class GamePanelSurfaceGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Panels;
        public string ComponentName => "GamePanelSurface";

        public VisualElement CreateSample()
        {
            var row = PanelsAndDecorSampleSheet.CreateSampleRow();

            // hud は幅だけ決め高さは内容次第
            // The hud fixes only width; content decides height
            var hud = new GamePanelSurface("gallery-surface-hud", GamePanelSurfaceKind.Hud);
            var objective = new Label("Second Step");
            objective.AddToClassList("gallery-samples__objective");
            hud.Add(objective);
            row.Add(PanelsAndDecorSampleSheet.Cell(hud, "gallery-samples__challenge-hud"));

            // skit は親の下端に全幅で敷かれる
            // The skit band lays itself full-width at its parent's bottom
            var stage = new VisualElement();
            var skit = new GamePanelSurface("gallery-surface-skit", GamePanelSurfaceKind.Skit);
            skit.Add(new FadeRule());
            stage.Add(skit);
            row.Add(PanelsAndDecorSampleSheet.Cell(stage, "gallery-samples__skit-stage"));
            return row;
        }
    }
}
