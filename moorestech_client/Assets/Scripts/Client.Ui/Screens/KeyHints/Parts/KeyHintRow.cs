using Client.Game.InGame.UI.UIState.State;
using Client.Ui.Components.Text;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.KeyHints.Parts
{
    /// <summary>
    /// キーヒントの 1 行。kbd（KeyCap）と文言を横に並べ、どちらも言語の切り替えで引き直す
    /// One key-hint row: a kbd (KeyCap) and its text side by side, both re-resolved on language change
    /// </summary>
    /// <remarks>
    /// ShortcutHintLabel は「文の中の印をキーに置き換える」部品で、キー名と文言が別のキーで来るこの行には合わない（設計書 A-59）
    /// ShortcutHintLabel replaces a marker inside a sentence; it does not fit this row, whose key name and text arrive as separate keys (design A-59)
    /// </remarks>
    public sealed class KeyHintRow : VisualElement
    {
        private readonly LocalizedLabel _text;

        public KeyHintRow(KeyHint hint)
        {
            AddToClassList("key-hint-row");

            // キー名は束縛で引いて KeyCap へ流す。KeyCap は Label なので LocalizedLabel にはできない
            // Resolve the key name through a binding into KeyCap; KeyCap is a Label, so it cannot be a LocalizedLabel
            var keyCap = new KeyCap(string.Empty);
            var keyBinding = new LocalizedTextBinding(keyCap);
            keyBinding.OnResolved.Subscribe(keyCap.SetKeyText);
            keyBinding.SetText(new UiText(hint.KeyNameKey));
            Add(keyCap);

            _text = new LocalizedLabel();
            _text.AddToClassList("key-hint-row__text");
            _text.SetKey(hint.TextKey);
            Add(_text);
        }

        // 25px の字の自然な高さは Web の行高 30px を超えるので、行と文言を同じ固定の高さにする（判断記録 D-22）
        // A 25px glyph is naturally taller than the Web 30px line height, so the row and its text share a fixed height (decision D-22)
        public void SetRowHeight(float height)
        {
            style.height = height;
            _text.style.height = height;
        }
    }
}
