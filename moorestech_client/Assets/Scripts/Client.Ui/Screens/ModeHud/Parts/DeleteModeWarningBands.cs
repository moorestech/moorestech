using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Foundation.Anchor;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.ModeHud.Parts
{
    /// <summary>
    /// 削除モードの上下の警告帯 2 本
    /// The delete mode's top and bottom warning bands
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
