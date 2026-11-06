using UnityEngine.UIElements;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// IME の未確定文字列の下に 1px の線を引く。キャレットを右端にして未確定文字列の幅だけ左へ伸ばす
    /// Draws a 1px line under the IME composition string, ending at the caret and extending left by the composition width
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

        // 前回見た未確定文字列から変わったときだけ引き直す（毎フレームの呼び出しを安くする）
        // Redraw only when the observed composition changed since last time, keeping per-frame calls cheap
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

        public void Apply(string compositionString)
        {
            if (compositionString.Length == 0)
            {
                _line.style.display = DisplayStyle.None;
                return;
            }

            // 未確定文字列の幅を測り、行の下端を指すキャレットから左へ伸ばす。文字の領域より左にははみ出させない
            // Measure the composition width and extend left from the caret (which points at the line bottom), never past the text area
            var width = _textElement.MeasureTextSize(compositionString, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x;
            var caret = _field.textSelection.cursorPosition;
            var left = UnityEngine.Mathf.Max(_textElement.contentRect.xMin, caret.x - width);

            // TextElement の座標を線の親の座標へ写す（単行の横スクロールもこの時点の位置で反映される）。absolute の left/top は親の枠線の内側から測るので枠線ぶん引く
            // Map TextElement coordinates into the line's parent space (picks up the current single-line scroll offset); absolute left/top start inside the parent's border, so subtract it
            var lineParent = _line.hierarchy.parent;
            var start = _textElement.ChangeCoordinatesTo(lineParent, new UnityEngine.Vector2(left, caret.y - Thickness));
            var end = _textElement.ChangeCoordinatesTo(lineParent, caret);
            _line.style.left = start.x - lineParent.resolvedStyle.borderLeftWidth;
            _line.style.top = start.y - lineParent.resolvedStyle.borderTopWidth;
            _line.style.width = end.x - start.x;

            // 色はテーマや画面ごとに変わる文字色に合わせる（USS に currentColor が無いので解決済みの値を写す）
            // Match the text colour, which varies by theme and screen (USS has no currentColor, so copy the resolved value)
            _line.style.backgroundColor = _textElement.resolvedStyle.color;
            _line.style.display = DisplayStyle.Flex;
        }
    }
}
