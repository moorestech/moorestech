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
    /// 言語購読と文が無いときの報告は LocalizedTextBinding に任せる。
    /// USS で --localized-label-line-height を宣言したラベルは、Web の line-height と同じく行送りをその px にし、高さを行数×その px にする
    /// （USS に line-height が無いため、行送りは <line-height> を前に付け、高さは測った結果を行の箱へ丸める。字は -unity-text-align で箱の中央に置かれ、Web の half-leading と同じ位置になる）
    /// Language subscription and missing-text reporting are delegated to LocalizedTextBinding.
    /// A label whose USS declares --localized-label-line-height spaces its lines by that px and sizes its height to line count x that px, like the Web line-height
    /// (USS has no line-height, so a <line-height> tag sets the pitch and the measured height snaps to the line boxes; -unity-text-align centres the glyphs in the box, matching the Web half-leading)
    /// </remarks>
    public class LocalizedLabel : Label
    {
        private const string LineHeightProperty = "--localized-label-line-height";
        private static readonly CustomStyleProperty<string> LineHeightStyle = new(LineHeightProperty);

        private readonly LocalizedTextBinding _binding;
        private string _resolvedText;
        private string _lineHeightTag = string.Empty;
        private float _lineHeightPixels;

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
            if (_lineHeightPixels <= 0f || heightMode == MeasureMode.Exactly) return measured;

            // 測った高さ＝(行数−1)×行送り＋1 行目の字の高さ。字の高さは行の高さの 0.5〜1.5 倍に収まるので四捨五入で行数になる
            // Measured height = (lines - 1) x pitch + first-line glyph extent; that extent is 0.5-1.5 line heights, so rounding yields the line count
            var lineCount = Mathf.Max(1, Mathf.RoundToInt(measured.y / _lineHeightPixels));
            return new Vector2(measured.x, lineCount * _lineHeightPixels);
        }

        private void OnCustomStyleResolved()
        {
            _lineHeightPixels = ReadLineHeightPixels();
            _lineHeightTag = _lineHeightPixels > 0f ? $"<line-height={_lineHeightPixels.ToString(CultureInfo.InvariantCulture)}px>" : string.Empty;
            ShowResolvedText();

            #region Internal

            float ReadLineHeightPixels()
            {
                // silent-ok: 行送りを指定しないラベルが大半 / 指定した部品はその部品のテスト（TooltipFaceTest・TypographyLineHeightTest）が行送りを確かめる
                if (!customStyle.TryGetValue(LineHeightStyle, out var value)) return 0f;

                // 宣言したのに長さとして読めなければ開発者に知らせ、行送りは既定に戻す
                // A declared but unreadable value is reported and the pitch falls back to the default
                if (!UssTokenValue.TryParsePixels(value, out var pixels))
                {
                    Debug.LogError($"[LocalizedLabel] {LineHeightProperty} is not a resolved length (got '{value}'): {name}");
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
