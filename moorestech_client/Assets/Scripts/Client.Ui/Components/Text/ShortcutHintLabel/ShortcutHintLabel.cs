// moorestech_client/Assets/Scripts/Client.Ui/Components/Text/ShortcutHintLabel/ShortcutHintLabel.cs
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Text
{
    /// <summary>
    /// 翻訳の語順を保ったまま、文中に KeyCap を 1 つ差し込む（Web の shared/i18n/LocalizedShortcutHint）
    /// Inserts one KeyCap into a sentence while keeping the translated word order (Web shared/i18n/LocalizedShortcutHint)
    /// </summary>
    /// <remarks>
    /// 文の解決と言語変更の解き直しは Foundation の LocalizedTextBinding が持ち、ここは解いた文を割って並べるだけ（D-18）
    /// Resolving and re-resolving on language change belong to Foundation's LocalizedTextBinding; this only splits and lays out the result (D-18)
    /// </remarks>
    public sealed class ShortcutHintLabel : VisualElement
    {
        private const string FollowingClass = "shortcut-hint-label__item--following";
        private readonly ShortcutHintLayout _layout;
        private readonly Label _before;
        private readonly KeyCap _keyCap;
        private readonly Label _after;
        private LocalizedTextBinding _sentence;

        public ShortcutHintLabel(string testid, ShortcutHintLayout layout)
        {
            name = testid;
            _layout = layout;
            pickingMode = PickingMode.Ignore;
            AddToClassList("shortcut-hint-label");
            _before = CreateText();
            _keyCap = new KeyCap("");
            _after = CreateText();
            Add(_before);
            Add(_keyCap);
            Add(_after);
        }

        public void SetHint(UiText sentence, string shortcutText)
        {
            // {shortcut} に印を入れた文を渡す。印を先頭に置くので、呼び出し側が shortcut を渡しても印が勝つ
            // Hand over the sentence with the marker in {shortcut}; the marker goes first, so it wins over a caller-supplied shortcut
            _keyCap.SetKeyText(shortcutText);
            EnsureSentenceBinding();
            _sentence.SetText(sentence.WithLeadingParam(ShortcutHintSegments.ParamName, ShortcutHintSegments.Marker));
        }

        private void EnsureSentenceBinding()
        {
            // 文が来る SetHint で束縛を作る。載せた後に SetHint する使い方でも、文無しの載りを誤って LogError にしない
            // Create the binding at SetHint, when the sentence arrives, so mounting before SetHint is not reported as a missing sentence
            if (_sentence != null) return;
            _sentence = new LocalizedTextBinding(this);
            _sentence.OnResolved.Subscribe(ShowResolved);
        }

        private void ShowResolved(string resolved)
        {
            // 印の位置で前後に割る。訳の欠けは Localize が [!key] の可視文字列で返すので空の分岐は持たない（D-20）
            // Split at the marker; a missing translation comes back as a visible [!key], so there is no empty branch (D-20)
            var segments = ShortcutHintSegments.Split(resolved, _layout);
            if (segments.Kind == ShortcutHintSplitKind.KeyFirstBecauseMarkerMissing) Debug.LogError($"[ShortcutHintLabel] {name}: the sentence has no shortcut marker; showing the key first");
            ShowText(_before, segments.Before);
            ShowText(_after, segments.After);
            MarkFollowingItems();

            #region Internal

            void ShowText(Label label, string value)
            {
                label.text = value;
                label.style.display = value.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            }

            void MarkFollowingItems()
            {
                // 表示中の先頭以外に間隔のクラスを付ける（Web は置く側の flex の gap）
                // Mark every shown item except the first for the gap (the Web uses the host's flex gap)
                var foundFirstShown = false;
                foreach (var item in Children())
                {
                    if (item.style.display == DisplayStyle.None) continue;
                    item.EnableInClassList(FollowingClass, foundFirstShown);
                    foundFirstShown = true;
                }
            }

            #endregion
        }

        private static Label CreateText()
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList("shortcut-hint-label__text");
            return label;
        }
    }
}
