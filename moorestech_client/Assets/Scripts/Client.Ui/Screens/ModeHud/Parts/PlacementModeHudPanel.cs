using System.Collections.Generic;
using Client.Localization;
using Client.Ui.Components.Decor;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Text;
using Client.Ui.State.ModeHud;
using Client.Ui.State.Text;
using Client.Game.InGame.Tutorial.UIHighlight;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.ModeHud.Parts
{
    /// <summary>
    /// 右上の配置モード HUD
    /// The top-right placement HUD
    /// </summary>
    public sealed class PlacementModeHudPanel : VisualElement
    {
        private readonly LocalizedLabel _selected;
        private readonly LocalizedLabel _height;

        public PlacementModeHudPanel()
        {
            name = "placement-mode-hud";
            AddToClassList("mode-hud__corner");
            TutorialAnchor.Mark(this, AnchorIds.PlacementHud);

            // 枠 → 見出し（題・罫線）→ 詳細の列
            // Frame → heading (title, rule) → detail list
            var panel = new GamePanel("placement-mode-hud-panel", GamePanelFrame.Craft);
            var heading = new HudSectionHeading("operation-mode-label", LocalizationKeys.Ui.ModeHud.PlacementModeTitle);
            panel.Add(heading);
            Add(panel);

            _selected = Detail("operation-mode-detail", false);
            _height = Detail("operation-mode-detail", true);
            // 置けない理由の行（Web の .warning）は持たない。実ゲームでは理由の書き手が無い（look-exception: LX-191）
            // No unavailable-reason line (Web .warning); nothing writes a reason in the real game (look-exception: LX-191)
            heading.Add(_selected);
            heading.Add(_height);

            #region Internal

            static LocalizedLabel Detail(string testid, bool following)
            {
                var label = new LocalizedLabel { name = testid };
                label.AddToClassList("mode-hud__detail");
                if (following) label.AddToClassList("mode-hud__detail--following");
                return label;
            }

            #endregion
        }

        public void Show(ModeHudPlacement placement)
        {
            // silent-ok: 対象が未選択の瞬間は Web と同じく名前を空で「Selected: 」と出す / 次の OnTargetChanged で名前が入り HUD を見る人が気づく
            var targetName = placement.Target.HasValue ? placement.Target.Value : "";
            _selected.SetText(new UiText(LocalizationKeys.Ui.ModeHud.SelectedBlock, new[] { new LocalizationTextParam("name", targetName) }));
            _height.SetText(PlacementHeightText.Create(placement.Height));
        }
    }
}
