using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Components.Decor;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Challenge;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Challenge.CurrentHud
{
    /// <summary>
    /// 実画面の左上の進行中チャレンジ HUD（Web の CurrentChallengeHud）。hud の面に見出しと目標の列を載せる
    /// Current challenge HUD at the viewport top-left (Web CurrentChallengeHud): a heading and objective column on the hud face
    /// </summary>
    public sealed class CurrentChallengeHudView : VisualElement
    {
        private readonly UiStack _objectives;

        public CurrentChallengeHudView()
        {
            name = "challenge-hud";
            AddToClassList("current-challenge-hud");
            TutorialAnchor.Mark(this, AnchorIds.ChallengeCurrentHud);

            // 面（hud）→ 見出し（題・罫線）→ 目標の列。目標の間隔は Web の --challenge-hud-objective-gap（2px）
            // Face (hud), then heading (title and rule), then the objectives; objective spacing is the Web --challenge-hud-objective-gap (2px)
            var surface = new GamePanelSurface("challenge-hud-surface", GamePanelSurfaceKind.Hud);
            var heading = new HudSectionHeading("challenge-hud-label", new UiText(LocalizationKeys.Ui.Challenge.CurrentTitle));
            _objectives = new UiStack(UiGap.Px2);
            _objectives.AddToClassList("current-challenge-hud__objectives");
            heading.Add(_objectives);
            surface.Add(heading);
            Add(surface);
            IgnorePicking();
            style.display = DisplayStyle.None;
        }

        public void Render(CurrentChallengeHudViewModel model)
        {
            style.display = model.Shown ? DisplayStyle.Flex : DisplayStyle.None;
            _objectives.ClearItems();
            foreach (var objective in model.Objectives)
            {
                var label = new LocalizedLabel { name = "challenge-objective" };
                label.AddToClassList("current-challenge-hud__objective");
                label.SetText(objective);
                _objectives.AddItem(label);
            }
            IgnorePicking();
        }

        // 常駐 HUD はどの要素も入力を奪わない（Web の pointer-events: none）。部品の内側も含める
        // No element of a resident HUD takes input (Web pointer-events: none), including the components' insides
        private void IgnorePicking()
        {
            UiPickingIgnore.ApplyToSubtree(this);
        }
    }
}
