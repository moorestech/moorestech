using UnityEngine.UIElements;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// 未確定文字列の下に1px線を引く
    /// Draw a 1px line under the composition
    /// </summary>
    /// <remarks>
    /// 複数行で折り返しをまたぐ未確定文字列は、キャレットのある行ぶんだけ引く割り切り
    /// A composition that wraps across lines in multiline mode is only underlined on the caret's line, by design
    /// </remarks>
    internal sealed class ImeCompositionUnderline
    {
        public const string UssClassName = "game-text-field__composition-underline";
        private const float Thickness = 1f;

        private readonly UnityEngine.UIElements.TextField _field;
        private readonly TextElement _textElement;
        private string _lastObservedComposition = string.Empty;

        private readonly VisualElement _line;

        public ImeCompositionUnderline(UnityEngine.UIElements.TextField field, TextElement textElement)
        {
            _field = field;
            _textElement = textElement;

            // TextElement に子を足すと文字の計測が効かず単行の高さが 0 になり文字が消えるので、兄弟として置く
            // Adding a child to the TextElement disables its text measuring (single-line height collapses to 0), so place the line as a sibling
            _line = new VisualElement { pickingMode = PickingMode.Ignore };
            _line.AddToClassList(UssClassName);
            _line.style.position = Position.Absolute;
            _line.style.height = Thickness;
            _line.style.display = DisplayStyle.None;
            _textElement.hierarchy.parent.Add(_line);
        }

        // 変化時のみ引き直す
        // Redraw only on change
        public void Refresh(string observedComposition)
        {
            if (observedComposition == _lastObservedComposition) return;
            _lastObservedComposition = observedComposition;
            Apply(observedComposition);
        }

        // フォーカスアウトで変換は終わるので、次のフォーカスは空から見始める
        // Focus-out ends the composition, so the next focus starts observing from empty
        public void Reset()
        {
            _lastObservedComposition = string.Empty;
            Apply(string.Empty);
        }

        private void Apply(string compositionString)
        {
            if (compositionString.Length == 0)
            {
                _line.style.display = DisplayStyle.None;
                return;
            }

            // キャレットから左へ伸ばす
            // Extend left from the caret
            var width = _textElement.MeasureTextSize(compositionString, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x;
            var caret = _field.textSelection.cursorPosition;
            var left = UnityEngine.Mathf.Max(_textElement.contentRect.xMin, caret.x - width);

            // 親座標へ写す（枠線ぶん引く）
            // Map to parent space minus the border
            var lineParent = _line.hierarchy.parent;
            var start = _textElement.ChangeCoordinatesTo(lineParent, new UnityEngine.Vector2(left, caret.y - Thickness));
            var end = _textElement.ChangeCoordinatesTo(lineParent, caret);
            _line.style.left = start.x - lineParent.resolvedStyle.borderLeftWidth;
            _line.style.top = start.y - lineParent.resolvedStyle.borderTopWidth;
            _line.style.width = end.x - start.x;

            // 解決済みの文字色を写す
            // Copy the resolved text color
            _line.style.backgroundColor = _textElement.resolvedStyle.color;
            _line.style.display = DisplayStyle.Flex;
        }
    }
}
