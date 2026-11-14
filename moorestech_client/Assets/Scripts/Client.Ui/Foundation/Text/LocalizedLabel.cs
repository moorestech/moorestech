using System.Globalization;
using Client.Ui.Foundation.Text.SizeRounding;
using Client.Ui.Foundation.Text.WrapSafe;
using Client.Ui.Foundation.Theme;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Text
{
    /// <summary>
    /// UiTextを持つ言語追従ラベル
    /// Label holding UiText that follows language
    /// </summary>
    /// <remarks>
    /// 言語購読と文が無いときの報告は LocalizedTextBinding に任せる。USS に line-height が無いので、行の高さは 2 つのつまみで受け、行送りは <line-height> を前に付けて写す。
    /// --localized-label-line-box は Web の line-height そのもの: 行送りをその px にし、高さを行数×その px にする（字は -unity-text-align で行の箱の中央に置かれ Web の half-leading と同じ位置。Typography のクラスが宣言する）。
    /// --localized-label-line-height は行送りだけ: 1 行目は字の自然な高さのまま（画面が個別に宣言する。両方あればこちらを採る）
    /// 行末の閉じ約物は LineEndPunctuationTrimmer が Chrome の text-spacing-trim: normal と同じく半角に詰めて折り返す（設計書 A-223）
    /// 字の大きさの物理 px への丸めで広がった字の送りは、RoundedFontSizeSpacing の字間の印を前置きに足して Web の送りへ戻す（設計書 A-225）
    /// Language subscription and missing-text reporting are delegated to LocalizedTextBinding. USS has no line-height, so two knobs carry it and a <line-height> tag sets the pitch.
    /// --localized-label-line-box is the Web line-height itself: the pitch is that px and the height is line count x that px (-unity-text-align centres glyphs in the line box like the Web half-leading; declared by the Typography classes).
    /// --localized-label-line-height sets only the pitch, keeping the natural first-line height (declared per screen; it wins when both are present)
    /// LineEndPunctuationTrimmer wraps with line-end closing punctuation trimmed to half width like Chrome's text-spacing-trim: normal (design A-223)
    /// Advances widened by rounding the font size to physical px return to the Web advance through RoundedFontSizeSpacing's spacing tag added to the prefix (design A-225)
    /// </remarks>
    public class LocalizedLabel : WrapSafeLabel
    {
        private const string LineHeightProperty = "--localized-label-line-height";
        private const string LineBoxProperty = "--localized-label-line-box";
        private static readonly CustomStyleProperty<string> LineHeightStyle = new(LineHeightProperty);
        private static readonly CustomStyleProperty<string> LineBoxStyle = new(LineBoxProperty);

        private readonly LocalizedTextBinding _binding;
        private readonly LineEndPunctuationTrimmer _lineEndPunctuationTrimmer;
        private readonly RoundedFontSizeSpacing _roundedFontSizeSpacing;
        private string _lineHeightTag = string.Empty;
        private float _lineBoxPixels;

        public LocalizedLabel()
        {
            _binding = new LocalizedTextBinding(this);
            _lineEndPunctuationTrimmer = new LineEndPunctuationTrimmer(this, WhiteSpaceHolds);
            _roundedFontSizeSpacing = new RoundedFontSizeSpacing(this);
            _binding.OnResolved.Subscribe(resolved =>
            {
                SourceText = resolved;
                ShowResolvedText();
            });
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyleResolved());
        }

        // 解決した原文。text は行送りの印と行末の約物の詰めの改行を含む表示文なので、文の中身はこちらで読む
        // The resolved source text; text is the display text with the line-height tag and trim newlines, so read the words here
        public string SourceText { get; private set; }

        // 文が届く前のラベルは何も描いていないので空と読む
        // A label whose text has not arrived draws nothing, so it reads as empty
        public override string WordsText => SourceText ?? string.Empty;

        public void SetKey(LocalizationKey key)
        {
            SetText(new UiText(key));
        }

        public void SetText(UiText text)
        {
            _binding.SetText(text);
        }

        // 字の高さ（和文 1.448em を画素へ切り上げた値）でなく、Web と同じ「行数×行の高さ」を自分の高さにする
        // Size to line count x line height like the Web, not to the glyph extent (CJK 1.448em ceiled to pixels)
        protected override Vector2 DoMeasure(float desiredWidth, MeasureMode widthMode, float desiredHeight, MeasureMode heightMode)
        {
            var measured = _lineEndPunctuationTrimmer.ToSourceSize(base.DoMeasure(desiredWidth, widthMode, desiredHeight, heightMode), desiredWidth, widthMode);
            // silent-ok: 行の高さを宣言しないラベルは字の高さのまま / 宣言した Typography のクラスは TypographyLineHeightTest が高さを確かめる
            if (_lineBoxPixels <= 0f || heightMode == MeasureMode.Exactly) return measured;

            // 測った高さ＝(行数−1)×行送り＋1 行目の字の高さ。字の高さは行の高さの 0.5〜1.5 倍に収まるので四捨五入で行数になる
            // Measured height = (lines - 1) x pitch + first-line glyph extent; that extent is 0.5-1.5 line heights, so rounding yields the line count
            var lineCount = Mathf.Max(1, Mathf.RoundToInt(measured.y / _lineBoxPixels));
            // 1 行は同じ値を持つ Typography の min-height に任せ、行の箱をスタイルの経路で決める（測った値の経路にすると設定画面の縦が 2.9px 伸びた）
            // One line is left to the Typography min-height of the same value so the style path sizes it (the measured path grew the settings page by 2.9px)
            if (lineCount == 1) return new Vector2(measured.x, 0f);
            return new Vector2(measured.x, lineCount * _lineBoxPixels);
        }

        // 1 行の自然幅。表示文は行末の約物の詰めで改行を含むので原文で測り、字間の印だけを前置きして描画と同じ送りにする
        // One-line natural width; the display text holds trim newlines so measure the source, prefixed only with the spacing tag to match the rendered advance
        public float MeasureSingleLineWidth()
        {
            // silent-ok: 文が来る前は幅 0 / 来なければ LocalizedTextBinding が LogError を出す
            if (SourceText == null) return 0f;
            var prefixed = _lineHeightTag + _roundedFontSizeSpacing.TagFor(SourceText) + SourceText;
            return MeasureTextSize(prefixed, 0, MeasureMode.Undefined, 0, MeasureMode.Undefined).x;
        }

        private void OnCustomStyleResolved()
        {
            // 行送りのつまみ優先、無ければ行の箱で決める
            // A screen pitch knob sets only the pitch; otherwise the Typography line box sets both pitch and height
            var pitch = ReadPixels(LineHeightStyle, LineHeightProperty);
            _lineBoxPixels = 0f < pitch ? 0f : ReadPixels(LineBoxStyle, LineBoxProperty);
            if (pitch <= 0f) pitch = _lineBoxPixels;
            _lineHeightTag = 0f < pitch ? $"<line-height={pitch.ToString(CultureInfo.InvariantCulture)}px>" : string.Empty;
            // 倍率・字の大きさの変化で字間の印も変わりうる。前置きと原文が同じなら LineEndPunctuationTrimmer.Show が何もしない（文を入れ直すと詰めが振り出しへ戻るため）
            // A scale or font size change may change the spacing tag too; LineEndPunctuationTrimmer.Show ignores an unchanged prefix and source (re-setting the text would restart the trim)
            ShowResolvedText();

            #region Internal

            float ReadPixels(CustomStyleProperty<string> style, string propertyName)
            {
                // silent-ok: 行の高さを指定しないラベルが大半 / 指定した部品・クラスはそのテスト（TooltipFaceTest・TypographyLineHeightTest）が行送りを確かめる
                if (!customStyle.TryGetValue(style, out var value)) return 0f;

                // 宣言したのに長さとして読めなければ開発者に知らせ、行送りは既定に戻す
                // A declared but unreadable value is reported and the pitch falls back to the default
                if (!UssTokenValue.TryParsePixels(value, out var pixels))
                {
                    Debug.LogError($"[LocalizedLabel] {propertyName} is not a resolved length (got '{value}'): {name}");
                    return 0f;
                }
                return pixels;
            }

            #endregion
        }

        private void ShowResolvedText()
        {
            // silent-ok: 文が来るまで出さない / 載って 1 フレーム後も無ければ LocalizedTextBinding が LogError を出す
            if (SourceText == null) return;
            _lineEndPunctuationTrimmer.Show(_lineHeightTag + _roundedFontSizeSpacing.TagFor(SourceText), SourceText);
        }
    }
}
