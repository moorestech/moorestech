using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Text.SizeRounding
{
    /// <summary>
    /// 字の大きさが物理画素の整数へ丸まって広がった字の送りを、文の前に付ける負の字間の印で Web の送りへ戻す
    /// Returns glyph advances widened by rounding the font size to whole physical pixels to the Web advance, with a negative spacing tag put before the text
    /// </summary>
    /// <remarks>
    /// standard の文字生成は「字の大きさ×倍率」を物理 px の整数へ丸めて字を組む（倍率 1.5 の 15px は 22.5→23。Chrome は端数のまま組む）。字間（UITK の単位は字の大きさの 1/100）も丸めた大きさで効くので、行の幅は丸めの比で一様に広がり、Web より 1 行多く折れることがある。
    /// 文を 1 行で測った幅から広がった分を出し、印の付いた字の数で割った量を `cspace`（字の送りに足す印）で引く。字の形は丸めた大きさのまま。丸めで大きさが変わらない（倍率 1 の整数 px 等）なら印を付けない。設計書 A-225
    /// The standard generator lays glyphs out at font size x scale rounded to whole physical px (15px at scale 1.5 is 22.5 -> 23; Chrome keeps the fraction). Letter spacing (UITK units are 1/100 of the font size) also acts at the rounded size, so line widths widen by the rounding ratio and may wrap one line more than the Web.
    /// The widening is taken from the single-line width of the text and subtracted as a cspace tag (added to every advance) divided by the spaced glyph count; glyph shapes stay at the rounded size. No tag when rounding keeps the size (whole px at scale 1 and so on). Design A-225
    /// </remarks>
    internal sealed class RoundedFontSizeSpacing
    {
        // 印の付いた字の数を、この量の cspace で広がった幅から数える
        // Count spaced glyphs from the width this much cspace adds
        private const float CountingSpacingPixels = 10f;
        private static readonly string CountingTag = $"<cspace={CountingSpacingPixels.ToString(CultureInfo.InvariantCulture)}px>";

        private readonly TextElement _element;
        private bool _hasLastInput;
        private string _lastText;
        private float _lastPixelsPerPoint;
        private LineEndTextStyle _lastStyle;
        private string _lastTag = string.Empty;

        public RoundedFontSizeSpacing(TextElement element)
        {
            _element = element;
        }

        // 文・倍率・字のスタイルが前回と同じなら測らずに前の印を返す
        // Return the previous tag without measuring while the text, scale and text style are unchanged
        public string TagFor(string text)
        {
            // silent-ok: パネルに載る前は倍率が無い / 載った後のスタイルの解決で LocalizedLabel が問い直す
            if (_element.panel == null) return string.Empty;
            var pixelsPerPoint = _element.scaledPixelsPerPoint;
            var style = LineEndTextStyle.Read(_element);
            if (_hasLastInput && text == _lastText && Mathf.Approximately(pixelsPerPoint, _lastPixelsPerPoint) && style.SameAs(_lastStyle)) return _lastTag;

            _hasLastInput = true;
            _lastText = text;
            _lastPixelsPerPoint = pixelsPerPoint;
            _lastStyle = style;
            _lastTag = ComputeTag(text, pixelsPerPoint);
            return _lastTag;
        }

        private string ComputeTag(string text, float pixelsPerPoint)
        {
            // UITKTextHandle と同じ丸め（AwayFromZero）で、文字生成が使う物理 px の大きさを出す
            // Same rounding as UITKTextHandle (AwayFromZero) gives the physical size the generator uses
            var exactPixels = _element.resolvedStyle.fontSize * pixelsPerPoint;
            var roundedPixels = (float)Math.Round(exactPixels, MidpointRounding.AwayFromZero);
            // silent-ok: 丸めで大きさが変わらなければ送りも Web と同じ / RoundedFontSizeSpacingTest が倍率 1 で印が無いことを確かめる
            if (roundedPixels < 1f || Mathf.Abs(roundedPixels - exactPixels) < 0.001f) return string.Empty;

            // 1 行で測った幅と、字ごとに 10px 足した幅の差から字の数を出す（改行のある文は一番長い行）
            // Glyph count comes from the single-line width and the width with 10px per glyph (the longest line for multi-line text)
            var width = MeasureSingleLine(text);
            var spacedGlyphs = (MeasureSingleLine(CountingTag + text) - width) / CountingSpacingPixels;
            // silent-ok: 空の文・印だけの文は送りが無い / 字を含む文に替われば TagFor が測り直す
            if (spacedGlyphs < 0.5f) return string.Empty;

            // 広がった分＝幅×(1−本来の大きさ／丸めた大きさ)。字ごとに等分して引く。字の形は丸めた大きさのまま残る
            // The widening is width x (1 - exact / rounded), subtracted evenly per glyph; glyph shapes stay at the rounded size
            var excessPerGlyph = width * (1f - exactPixels / roundedPixels) / spacedGlyphs;
            return $"<cspace={(-excessPerGlyph).ToString("0.####", CultureInfo.InvariantCulture)}px>"; // look-exception: LX-185
        }

        private float MeasureSingleLine(string text)
        {
            return _element.MeasureTextSize(text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined).x;
        }
    }
}
