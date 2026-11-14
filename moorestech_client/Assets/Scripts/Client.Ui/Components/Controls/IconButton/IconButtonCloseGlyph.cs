using Client.Ui.Foundation.Theme;
using Client.Ui.Foundation.Theme.Mesh;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// IconButton 既定の ×（16角の2本線）
    /// IconButton default cross (two strokes in 16x16)
    /// </summary>
    public sealed class IconButtonCloseGlyph : VisualElement
    {
        public const string UssClassName = "icon-button__close-glyph";

        // Web の CloseIcon の viewBox と線の端点（2 本ぶん）
        // Web CloseIcon viewBox and the stroke endpoints (two strokes)
        private const float ViewBoxSize = 16f;
        private static readonly Vector2[] StrokeEnds = { new(3f, 3f), new(13f, 13f), new(13f, 3f), new(3f, 13f) };

        private Color _strokeColor;
        private float _strokeWidth;
        private bool _hasStyle;

        public IconButtonCloseGlyph()
        {
            AddToClassList(UssClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += DrawCross;
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyleResolved());
        }

        private void OnCustomStyleResolved()
        {
            // ホバーで色が変わるたびに解決し直されるので、読み直して描き直す
            // Hover re-resolves the colour each time, so re-read and redraw
            var hasColor = UssCustomStyleReader.TryReadColor(customStyle, IconButton.GlyphColorProperty, out _strokeColor);
            var hasWidth = UssCustomStyleReader.TryReadPixels(customStyle, IconButton.GlyphStrokeProperty, out _strokeWidth);
            _hasStyle = hasColor && hasWidth;
            MarkDirtyRepaint();
        }

        private void DrawCross(MeshGenerationContext context)
        {
            // silent-ok: 色と線幅の解決前は描かない / 解決できなければ UssCustomStyleReader が LogError を出す
            // silent-ok: draw nothing until colour and width resolve / UssCustomStyleReader logs an error if they never do
            if (!_hasStyle) return;

            // viewBox の 16 を要素の幅へ拡大し、SVG と同じく線幅も同じ倍率で太らせる
            // Scale the 16-unit viewBox to the element width, and the stroke width with it as SVG does
            var scale = contentRect.width / ViewBoxSize;
            var painter = context.painter2D;
            painter.strokeColor = _strokeColor;
            painter.lineWidth = _strokeWidth * scale;
            painter.lineCap = LineCap.Butt;
            painter.BeginPath();
            for (var i = 0; i < StrokeEnds.Length; i += 2) Painter2DSquareCap.AddSegment(painter, StrokeEnds[i] * scale, StrokeEnds[i + 1] * scale, painter.lineWidth / 2f);
            painter.Stroke();
        }
    }
}
