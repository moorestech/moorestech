using UnityEngine.UIElements;

namespace Client.UitkPoc.Effects
{
    // 「CSSでの書き方・USSでの判定・見本・注記」を1枚に収めるカード
    // Card holding the CSS name, USS verdict, sample and note
    public class EffectCard : VisualElement
    {
        public readonly VisualElement Sample;

        public EffectCard(string cssName, EffectVerdict verdict, string note)
        {
            AddToClassList("fx-card");
            var header = new VisualElement();
            header.AddToClassList("fx-card__header");
            var title = new Label(cssName);
            title.AddToClassList("fx-card__title");
            var badge = new Label(verdict == EffectVerdict.Native ? "そのまま可" : verdict == EffectVerdict.Workaround ? "工夫で可" : "不可");
            badge.AddToClassList("fx-badge");
            badge.AddToClassList(verdict == EffectVerdict.Native ? "fx-badge--native" : verdict == EffectVerdict.Workaround ? "fx-badge--workaround" : "fx-badge--impossible");
            header.Add(title);
            header.Add(badge);
            Add(header);
            Sample = new VisualElement();
            Sample.AddToClassList("fx-card__sample");
            Add(Sample);
            var noteLabel = new Label(note);
            noteLabel.AddToClassList("fx-card__note");
            Add(noteLabel);
        }
    }

    public enum EffectVerdict
    {
        Native,
        Workaround,
        Impossible,
    }
}
