using UnityEngine.UIElements;

namespace Client.Ui.Components.TextField
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

        public VisualElement Line { get; }

        public ImeCompositionUnderline(UnityEngine.UIElements.TextField field, TextElement textElement)
        {
            _field = field;
            _textElement = textElement;

            // 単行の横スクロールで文字と一緒に動くよう、内側の TextElement の子に置く
            // Parent the line to the inner TextElement so it scrolls with the text in single-line mode
            Line = new VisualElement { pickingMode = PickingMode.Ignore };
            Line.AddToClassList(UssClassName);
            Line.style.position = Position.Absolute;
            Line.style.height = Thickness;
            Line.style.display = DisplayStyle.None;
            _textElement.Add(Line);
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
                Line.style.display = DisplayStyle.None;
                return;
            }

            // 未確定文字列の幅を測り、行の下端を指すキャレットから左へ伸ばす。文字の領域より左にははみ出させない
            // Measure the composition width and extend left from the caret (which points at the line bottom), never past the text area
            var width = _textElement.MeasureTextSize(compositionString, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x;
            var caret = _field.textSelection.cursorPosition;
            var left = UnityEngine.Mathf.Max(_textElement.contentRect.xMin, caret.x - width);

            Line.style.left = left;
            Line.style.top = caret.y - Thickness;
            Line.style.width = caret.x - left;
            // 色はテーマや画面ごとに変わる文字色に合わせる（USS に currentColor が無いので解決済みの値を写す）
            // Match the text colour, which varies by theme and screen (USS has no currentColor, so copy the resolved value)
            Line.style.backgroundColor = _textElement.resolvedStyle.color;
            Line.style.display = DisplayStyle.Flex;
        }
    }
}
