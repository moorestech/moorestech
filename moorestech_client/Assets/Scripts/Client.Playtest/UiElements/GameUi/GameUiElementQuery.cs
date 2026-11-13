using System.Collections.Generic;
using System.Text;
using Client.Ui.Foundation.Text.WrapSafe;
using UnityEngine.UIElements;

namespace Client.Playtest.UiElements.GameUi
{
    /// <summary>
    /// 渡されたパネルのルートから testid（name）で要素を照会し、文字を入れる純粋な関数群
    /// Pure functions that look up elements by testid (name) under a given panel root and set text on them
    /// </summary>
    internal static class GameUiElementQuery
    {
        public static UiElementSnapshot Find(VisualElement root, string testid, IGameUiScreenMapper mapper)
        {
            var matches = VisibleMatches(root, testid);
            if (matches.Count == 0) return UiElementSnapshot.NotFound(testid);
            if (1 < matches.Count) return UiElementSnapshot.Ambiguous(testid);

            var element = matches[0];
            var screenRect = mapper.PanelToScreen(element.worldBound);
            return UiElementSnapshot.Found(testid, true, IsPickedAtCenter(element), screenRect, ReadText(element), ReadStateFlags(element));

            #region Internal

            // 中心で Pick した要素が自身か子孫なら覆われていない
            // Not covered when the element picked at the center is itself or a descendant
            bool IsPickedAtCenter(VisualElement element)
            {
                var picked = element.panel.Pick(element.worldBound.center);
                return picked != null && (picked == element || element.Contains(picked));
            }

            string ReadText(VisualElement element)
            {
                if (element is TextField field) return field.value;
                if (element is TextElement textElement && !string.IsNullOrEmpty(WordsOf(textElement))) return WordsOf(textElement);

                // 箱と、文字を子のラベルへ持つボタンは子孫の文字を順に連結する（Web の textContent と同じ扱い）
                // A container or a button whose text lives in a child label concatenates descendant texts in order, like the Web textContent
                var builder = new StringBuilder();
                foreach (var descendant in element.Query<TextElement>().ToList()) builder.Append(WordsOf(descendant));
                return builder.ToString();
            }

            // 本番のラベルの text は表示用の印を含みうるので、Web の textContent に当たる WordsText を読む
            // A production label's text may carry display tags, so read WordsText, the Web textContent counterpart
            string WordsOf(TextElement textElement)
            {
                return textElement is WrapSafeLabel label ? label.WordsText : textElement.text;
            }

            // `--selected` のような修飾子クラスだけを状態名として返す
            // Only modifier classes such as `--selected` are returned as state names
            IReadOnlyCollection<string> ReadStateFlags(VisualElement element)
            {
                var flags = new List<string>();
                foreach (var className in element.GetClasses())
                {
                    if (className.Contains("--")) flags.Add(className);
                }
                return flags;
            }

            #endregion
        }

        public static UiSetTextResult SetText(VisualElement root, string testid, string text)
        {
            var matches = VisibleMatches(root, testid);
            if (matches.Count == 0) return new UiSetTextResult(UiSetTextStatus.NotFound, string.Empty);
            if (1 < matches.Count) return new UiSetTextResult(UiSetTextStatus.Ambiguous, string.Empty);
            if (matches[0] is not TextField field) return new UiSetTextResult(UiSetTextStatus.NotTextField, string.Empty);

            // 読み取り専用・無効は人が打てない欄なので書き換えない
            // Read-only or disabled fields cannot be typed into by a person, so leave them untouched
            if (field.isReadOnly || !field.enabledInHierarchy) return new UiSetTextResult(UiSetTextStatus.NotInteractable, field.value);

            // value への代入は maxLength で切られないので、人が打った時と同じく上限で切ってから入れる
            // Assigning value skips maxLength, so cut at the limit first, as typing by a person would
            var typed = 0 <= field.maxLength && field.maxLength < text.Length ? text.Substring(0, field.maxLength) : text;

            // SetValueWithoutNotify ではなく value へ代入し、購読側へ ChangeEvent を流す
            // Assign value rather than SetValueWithoutNotify so subscribers receive the ChangeEvent
            field.value = typed;
            var status = field.value == text ? UiSetTextStatus.Applied : UiSetTextStatus.ValueMismatch;
            return new UiSetTextResult(status, field.value);
        }

        // 非表示の要素は無いものとして扱う。所有を Web へ戻した後に隠れた UITK 要素が Web の同名要素と重複扱いになるのを防ぐ（Q は先頭 1 件だけなので Query で全件を数える）
        // Hidden elements count as absent, so a UITK element hidden after ownership returns to Web never duplicates the Web one (Query counts all; Q returns only the first)
        private static List<VisualElement> VisibleMatches(VisualElement root, string testid)
        {
            var matches = root.Query<VisualElement>(name: testid).ToList();
            matches.RemoveAll(element => !IsVisible(element));
            return matches;
        }

        // 自身と祖先の resolvedStyle がすべて見える状態で、かつ面積がある
        // Every resolvedStyle from self up the ancestors is shown, and the element has area
        internal static bool IsVisible(VisualElement element)
        {
            var bound = element.worldBound;
            if (!(0f < bound.width * bound.height)) return false;
            for (var current = element; current != null; current = current.hierarchy.parent)
            {
                var style = current.resolvedStyle;
                if (style.display == DisplayStyle.None) return false;
                if (style.visibility != Visibility.Visible) return false;
                if (!(0f < style.opacity)) return false;
            }
            return true;
        }
    }
}
