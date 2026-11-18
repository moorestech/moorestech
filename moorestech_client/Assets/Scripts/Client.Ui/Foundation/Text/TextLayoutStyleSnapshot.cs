using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Text
{
    /// <summary>
    /// 折り返し位置を決める文字のスタイル（字の大きさ・字間・語間・字形・太さ・描き方）の写し
    /// Snapshot of the text style that decides line breaks (size, letter and word spacing, font, weight, generator)
    /// </summary>
    /// <remarks>
    /// 測り役はラベルの子にできない（子を持つラベルは自分の文で大きさを測らなくなる）ので、ラベルの解決済みの値を inline で写す。値が前回と同じかで測り直すかも決める
    /// The measurer cannot be the label's child (a label with children stops sizing itself to its text), so the label's resolved values are copied inline; equality with the last snapshot also decides whether to re-measure
    /// </remarks>
    internal readonly struct TextLayoutStyleSnapshot
    {
        private readonly float _fontSize;
        private readonly float _letterSpacing;
        private readonly float _wordSpacing;
        private readonly float _paragraphSpacing;
        private readonly FontDefinition _fontDefinition;
        private readonly Font _font;
        private readonly FontStyle _fontStyle;
        private readonly TextGeneratorType _generator;

        private TextLayoutStyleSnapshot(IResolvedStyle resolved)
        {
            _fontSize = resolved.fontSize;
            _letterSpacing = resolved.letterSpacing;
            _wordSpacing = resolved.wordSpacing;
            _paragraphSpacing = resolved.unityParagraphSpacing;
            _fontDefinition = resolved.unityFontDefinition;
            _font = resolved.unityFont;
            _fontStyle = resolved.unityFontStyleAndWeight;
            _generator = resolved.unityTextGenerator;
        }

        public static TextLayoutStyleSnapshot Read(VisualElement element)
        {
            return new TextLayoutStyleSnapshot(element.resolvedStyle);
        }

        public void ApplyTo(IStyle style)
        {
            style.fontSize = _fontSize;
            style.letterSpacing = _letterSpacing;
            style.wordSpacing = _wordSpacing;
            style.unityParagraphSpacing = _paragraphSpacing;
            style.unityFontDefinition = new StyleFontDefinition(_fontDefinition);
            style.unityFont = new StyleFont(_font);
            style.unityFontStyleAndWeight = _fontStyle;
            style.unityTextGenerator = _generator;
        }

        public bool SameAs(TextLayoutStyleSnapshot other)
        {
            return Mathf.Approximately(_fontSize, other._fontSize)
                && Mathf.Approximately(_letterSpacing, other._letterSpacing)
                && Mathf.Approximately(_wordSpacing, other._wordSpacing)
                && Mathf.Approximately(_paragraphSpacing, other._paragraphSpacing)
                && _fontDefinition.Equals(other._fontDefinition)
                && _font == other._font
                && _fontStyle == other._fontStyle
                && _generator == other._generator;
        }
    }
}
