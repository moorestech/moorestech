using System.Globalization;
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
    /// Language subscription and missing-text reporting are delegated to LocalizedTextBinding. USS has no line-height, so two knobs carry it and a <line-height> tag sets the pitch.
    /// --localized-label-line-box is the Web line-height itself: the pitch is that px and the height is line count x that px (-unity-text-align centres glyphs in the line box like the Web half-leading; declared by the Typography classes).
    /// --localized-label-line-height sets only the pitch, keeping the natural first-line height (declared per screen; it wins when both are present)
    /// </remarks>
    public class LocalizedLabel : Label
    {
        private const string LineHeightProperty = "--localized-label-line-height";
        private const string LineBoxProperty = "--localized-label-line-box";
        private static readonly CustomStyleProperty<string> LineHeightStyle = new(LineHeightProperty);
        private static readonly CustomStyleProperty<string> LineBoxStyle = new(LineBoxProperty);

        private readonly LocalizedTextBinding _binding;
        private string _resolvedText;
        private string _lineHeightTag = string.Empty;
        private float _lineBoxPixels;

        public LocalizedLabel()
        {
            _binding = new LocalizedTextBinding(this);
            _binding.OnResolved.Subscribe(resolved =>
            {
                _resolvedText = resolved;
                ShowResolvedText();
            });
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyleResolved());
        }

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
            var measured = base.DoMeasure(desiredWidth, widthMode, desiredHeight, heightMode);
            // silent-ok: 行の高さを宣言しないラベルは字の高さのまま / 宣言した Typography のクラスは TypographyLineHeightTest が高さを確かめる
            if (_lineBoxPixels <= 0f || heightMode == MeasureMode.Exactly) return measured;

            // 測った高さ＝(行数−1)×行送り＋1 行目の字の高さ。字の高さは行の高さの 0.5〜1.5 倍に収まるので四捨五入で行数になる
            // Measured height = (lines - 1) x pitch + first-line glyph extent; that extent is 0.5-1.5 line heights, so rounding yields the line count
            var lineCount = Mathf.Max(1, Mathf.RoundToInt(measured.y / _lineBoxPixels));
            return new Vector2(measured.x, lineCount * _lineBoxPixels);
        }

        private void OnCustomStyleResolved()
        {
            // 画面の行送りのつまみがあれば行送りだけ、無ければ Typography の行の箱で行送りと高さを決める
            // A screen pitch knob sets only the pitch; otherwise the Typography line box sets both pitch and height
            var pitch = ReadPixels(LineHeightStyle, LineHeightProperty);
            _lineBoxPixels = pitch > 0f ? 0f : ReadPixels(LineBoxStyle, LineBoxProperty);
            if (pitch <= 0f) pitch = _lineBoxPixels;
            _lineHeightTag = pitch > 0f ? $"<line-height={pitch.ToString(CultureInfo.InvariantCulture)}px>" : string.Empty;
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
            if (_resolvedText == null) return;
            text = _lineHeightTag + _resolvedText;
        }
    }
}
