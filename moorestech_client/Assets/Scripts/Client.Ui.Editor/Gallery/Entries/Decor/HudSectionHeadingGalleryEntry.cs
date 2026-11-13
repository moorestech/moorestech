using Client.Ui.Components.Decor;
using Client.Ui.Components.Panels;
using Client.Ui.Editor.Gallery.Entries.Panels;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Decor
{
    /// <summary>
    /// HudSectionHeading の見本。参照 2-placement のチャレンジ HUD（hud の面）と設置モード HUD（craft）を同じ組み合わせで作る
    /// HudSectionHeading samples: the challenge HUD (hud face) and placement-mode HUD (craft) of reference 2-placement, built the same way
    /// </summary>
    public sealed class HudSectionHeadingGalleryEntry : IUiGalleryEntry
    {
        public UiGalleryFamily Family => UiGalleryFamily.Decor;
        public string ComponentName => "HudSectionHeading";

        public VisualElement CreateSample()
        {
            var row = PanelsAndDecorSampleSheet.CreateSampleRow();

            // チャレンジ HUD（hud の面）
            // Challenge HUD (hud face)
            var challenge = new GamePanelSurface("gallery-challenge-hud", GamePanelSurfaceKind.Hud);
            var challengeHeading = new HudSectionHeading("challenge-hud-label", new UiText(LocalizationKeys.Ui.Challenge.CurrentTitle));
            challengeHeading.Add(Line("Second Step", "gallery-samples__objective"));
            challenge.Add(challengeHeading);
            row.Add(PanelsAndDecorSampleSheet.Cell(challenge, "gallery-samples__challenge-hud"));

            // 設置モード HUD（craft の枠）
            // Placement-mode HUD (craft frame)
            var placement = new GamePanel("gallery-placement-hud", GamePanelFrame.Craft);
            var placementHeading = new HudSectionHeading("operation-mode-label", new UiText(LocalizationKeys.Ui.ModeHud.PlacementModeTitle));
            placementHeading.Add(Line("Selected: Assembler", "gallery-samples__mode-detail"));
            placementHeading.Add(Line("Height: 3", "gallery-samples__mode-detail"));
            placement.Add(placementHeading);
            row.Add(PanelsAndDecorSampleSheet.Cell(placement, "gallery-samples__mode-hud"));
            return row;

            #region Internal

            Label Line(string text, string className)
            {
                var line = new Label(text);
                line.AddToClassList(className);
                return line;
            }

            #endregion
        }
    }
}
