using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Skit.Parts.Toolbar
{
    /// <summary>
    /// ツールの図形を、影を下へずらした写しの上に本体を重ねて描く（Web の drop-shadow(0 1px 1px) のぼかし無しの近似。LX-52）
    /// Draws a tool glyph over a copy shifted down as its shadow (a blur-less approximation of the Web drop-shadow(0 1px 1px); LX-52)
    /// </summary>
    /// <remarks>
    /// 色は IconButton のつまみ（GlyphColorProperty）を読み、状態ごとの色は画面 USS が上書きする。線幅（単位なし）と影は自分の --skit-tool-glyph-* を読む。IconButton の線幅つまみは px なので共有しない（D-14・D-27・D-79）
    /// Colour comes from IconButton's knob (GlyphColorProperty), overridden per state by the screen USS; the unitless stroke and the shadow read this glyph's own --skit-tool-glyph-*, as IconButton's stroke knob is in px and is not shared (D-14, D-27, D-79)
    /// </remarks>
    public sealed class SkitToolGlyph : VisualElement
    {
        public const string UssClassName = "skit-tool-glyph";
        private const string StrokeProperty = "--skit-tool-glyph-stroke";

        private readonly SkitToolGlyphShape _shape;
        private Color _color;
        private float _stroke;
        private Color _shadowColor;
        private float _shadowOffset;
        private bool _hasTokens;

        public SkitToolGlyph(SkitToolGlyphShape shape)
        {
            _shape = shape;
            AddToClassList(UssClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyleResolved());
        }

        private void OnCustomStyleResolved()
        {
            // ホバー・無効・自動 ON のたびに解決し直されるので、読み直して描き直す（線幅は 24 単位の無次元の数）
            // Re-resolved on hover, disable and auto-on, so re-read and redraw (the stroke is a unitless number in 24 units)
            var hasColor = UssCustomStyleReader.TryReadColor(customStyle, IconButton.GlyphColorProperty, out _color);
            var hasStroke = UssCustomStyleReader.TryReadNumber(customStyle, StrokeProperty, out _stroke);
            var hasShadow = UssCustomStyleReader.TryReadColor(customStyle, "--skit-tool-glyph-shadow-color", out _shadowColor);
            var hasOffset = UssCustomStyleReader.TryReadPixels(customStyle, "--skit-tool-glyph-shadow-offset", out _shadowOffset);
            _hasTokens = hasColor && hasStroke && hasShadow && hasOffset;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            // silent-ok: トークンが読めない間は描かない / UssCustomStyleReader が読めなかったトークン名を LogError で出す
            if (!_hasTokens) return;

            // 24 単位の座標を要素の幅へ合わせ、線幅も同じ倍率で太らせる（SVG の stroke-width と同じ）
            // Fit the 24-unit coordinates to the element width and scale the stroke by the same factor (same as SVG stroke-width)
            var scale = contentRect.width / SkitToolGlyphPaths.ViewBoxSize;
            var painter = context.painter2D;
            painter.lineWidth = _stroke * scale;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;

            // 影を先に、本体をその上に描く
            // Draw the shadow first and the glyph on top
            painter.strokeColor = _shadowColor;
            SkitToolGlyphPaths.Stroke(painter, _shape, scale, new Vector2(0f, _shadowOffset)); // look-exception: LX-52
            painter.strokeColor = _color;
            SkitToolGlyphPaths.Stroke(painter, _shape, scale, Vector2.zero);
        }
    }
}
