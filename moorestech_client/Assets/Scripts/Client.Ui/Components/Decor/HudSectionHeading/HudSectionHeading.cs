using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Decor
{
    /// <summary>
    /// HUD の見出し。控えめな小ラベル → FadeRule → 本文の列（Web の CurrentChallengeHud と PlacementModeHud が同形で持つ組）
    /// HUD section heading: a subdued label, a FadeRule, then a body column (the shape Web CurrentChallengeHud and PlacementModeHud both repeat)
    /// </summary>
    /// <remarks>
    /// 寸法はつまみ --hud-label-font-size・--hud-label-letter-spacing・--hud-label-rule-gap・--hud-rule-list-gap・--hud-rule-width で受ける。既定はチャレンジ HUD の値で、操作モード HUD は画面 USS で --operation-hud-* へ上書きする
    /// Sizes come from the knobs --hud-label-font-size, --hud-label-letter-spacing, --hud-label-rule-gap, --hud-rule-list-gap and --hud-rule-width; defaults are the challenge HUD values and the operation-mode HUD overrides them with --operation-hud-* in its screen USS
    /// </remarks>
    public sealed class HudSectionHeading : VisualElement
    {
        private readonly VisualElement _list;

        public override VisualElement contentContainer => _list;

        public HudSectionHeading(string labelTestid, UiText label)
        {
            AddToClassList("hud-section-heading");

            // testid は Web と同じくラベルに付ける（challenge-hud-label・operation-mode-label）
            // The testid goes on the label as on the Web (challenge-hud-label, operation-mode-label)
            var labelElement = new LocalizedLabel { name = labelTestid };
            labelElement.AddToClassList("hud-section-heading__label");
            labelElement.SetText(label);
            hierarchy.Add(labelElement);

            // 罫線は包みの幅（つまみ --hud-rule-width）まで伸びる
            // The rule stretches to its holder's width (the --hud-rule-width knob)
            var ruleHolder = new VisualElement { pickingMode = PickingMode.Ignore };
            ruleHolder.AddToClassList("hud-section-heading__rule");
            ruleHolder.Add(new FadeRule());
            hierarchy.Add(ruleHolder);

            _list = new VisualElement();
            _list.AddToClassList("hud-section-heading__list");
            hierarchy.Add(_list);
        }
    }
}
