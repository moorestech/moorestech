using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Foundation.Anchor;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.ModeHud.Parts
{
    /// <summary>
    /// 削除モードの全面の入れ物と上下の帯 2 本。Web の DeleteModeWarningBands（下の帯に錨 delete.hud）
    /// Full-screen container with the top and bottom bands; Web DeleteModeWarningBands (bottom band anchored as delete.hud)
    /// </summary>
    public sealed class DeleteModeWarningBands : VisualElement
    {
        public DeleteModeWarningBands()
        {
            name = "delete-mode-warning";
            AddToClassList("mode-hud__warning");
            Add(new DeleteModeWarningBand("delete-mode-warning-band", "mode-hud__warning-band--top"));
            var bottom = new DeleteModeWarningBand("delete-mode-warning-band", "mode-hud__warning-band--bottom");
            TutorialAnchor.Mark(bottom, AnchorIds.DeleteHud);
            Add(bottom);
        }
    }
}
