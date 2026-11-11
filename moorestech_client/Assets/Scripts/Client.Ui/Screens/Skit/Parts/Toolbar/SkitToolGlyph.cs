// S/Client.Ui/Screens/Skit/Parts/Toolbar/SkitToolGlyph.cs
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
    /// 色と線幅は IconButton のつまみ（GlyphColorProperty・GlyphStrokeProperty）を読み、状態ごとの色は画面 USS が上書きする。影だけは IconButton に口が無いので自分の --skit-tool-glyph-shadow-* を読む（D-14・D-27）
    /// Colour and stroke come from IconButton's knobs (GlyphColorProperty, GlyphStrokeProperty), overridden per state by the screen USS; only the shadow reads this glyph's own --skit-tool-glyph-shadow-*, as IconButton has no entry for it (D-14, D-27)
    /// </remarks>
    public sealed class SkitToolGlyph : VisualElement
    {
        public const string UssClassName = "skit-tool-glyph";

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
            var hasStroke = UssCustomStyleReader.TryReadNumber(customStyle, IconButton.GlyphStrokeProperty, out _stroke);
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
            SkitToolGlyphPaths.Stroke(painter, _shape, scale, new Vector2(0f, _shadowOffset));
            painter.strokeColor = _color;
            SkitToolGlyphPaths.Stroke(painter, _shape, scale, Vector2.zero);
        }
    }
}
