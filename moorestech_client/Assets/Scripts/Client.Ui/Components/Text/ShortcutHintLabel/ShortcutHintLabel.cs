// moorestech_client/Assets/Scripts/Client.Ui/Components/Text/ShortcutHintLabel/ShortcutHintLabel.cs
using Client.Ui.Foundation.Text;
using Client.Ui.Foundation.Text.WrapSafe;
using Client.Ui.State.Text;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Text
{
    /// <summary>
    /// 文中に KeyCap を 1 つ差し込む
    /// Inserts one KeyCap into a sentence
    /// </summary>
    /// <remarks>
    /// 解決は LocalizedTextBinding、ここは割って並べる（D-18）
    /// Resolving is LocalizedTextBinding's; this splits and lays out (D-18)
    /// </remarks>
    public sealed class ShortcutHintLabel : VisualElement
    {
        private const string FollowingClass = "shortcut-hint-label__item--following";
        private readonly ShortcutHintLayout _layout;
        private readonly Label _before;
        private readonly KeyCap _keyCap;
        private readonly Label _after;
        private readonly LocalizedTextBinding _sentence;

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
            // 束縛は構築時に作る。SetHint が載せた後でも、載って 1 フレーム後も文が無ければ束縛が LogError を出す
            // Create the binding at construction; SetHint may follow mounting, and the binding reports a still-missing sentence one frame after mounting
            _sentence = new LocalizedTextBinding(this);
            _sentence.OnResolved.Subscribe(ShowResolved);

            #region Internal

            Label CreateText()
            {
                var label = new WrapSafeLabel { pickingMode = PickingMode.Ignore };
                label.AddToClassList("shortcut-hint-label__text");
                return label;
            }

            #endregion
        }

        public void SetHint(UiText sentence, string shortcutText)
        {
            // inline だけ {shortcut} に印を入れる。prefix は割らないので印は要らず、辞書キー以外の文もそのまま渡せる
            // Only inline puts the marker in {shortcut}; prefix never splits, so any text source passes through as is
            _keyCap.SetKeyText(shortcutText);
            _sentence.SetText(_layout == ShortcutHintLayout.Inline ? sentence.WithLeadingParam(ShortcutHintSegments.ParamName, ShortcutHintSegments.Marker) : sentence);
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
    }
}
