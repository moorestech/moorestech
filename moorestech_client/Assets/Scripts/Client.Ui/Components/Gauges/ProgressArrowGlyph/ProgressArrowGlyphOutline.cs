using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Gauges
{
    /// <summary>
    /// 矢印の path の輪郭の層（Web の .outline。miter・miterlimit 4 は SVG の既定）
    /// Outline layer of the arrow path (the Web .outline; miter and miterlimit 4 are SVG defaults)
    /// </summary>
    internal sealed class ProgressArrowGlyphOutline : VisualElement
    {
        private const string StrokeColorProperty = "--progress-arrow-glyph-layer-stroke";
        private const string StrokeWidthProperty = "--progress-arrow-glyph-layer-stroke-width";
        private const float SvgDefaultMiterLimit = 4f;

        private Color _strokeColor;
        private float _strokeWidth;
        private bool _hasStyle;

        public ProgressArrowGlyphOutline()
        {
            AddToClassList("progress-arrow-glyph__outline");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += DrawOutline;
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyleResolved());
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        private void OnCustomStyleResolved()
        {
            var hasColor = UssCustomStyleReader.TryReadColor(customStyle, StrokeColorProperty, out _strokeColor);
            var hasWidth = UssCustomStyleReader.TryReadPixels(customStyle, StrokeWidthProperty, out _strokeWidth);
            _hasStyle = hasColor && hasWidth;
            MarkDirtyRepaint();
        }

        private void DrawOutline(MeshGenerationContext context)
        {
            // silent-ok: 色と線幅の解決前は描かない / 解決できなければ UssCustomStyleReader が LogError を出す
            // silent-ok: draw nothing until color and width resolve / UssCustomStyleReader logs an error if they never do
            if (!_hasStyle) return;

            // SVG の stroke-width は viewBox の単位なので、線幅も path と同じ倍率で太らせる（IconButtonCloseGlyph と同じ）
            // An SVG stroke-width is in viewBox units, so the width scales with the path (as IconButtonCloseGlyph does)
            var scale = ProgressArrowGlyphPath.ScaleOf(contentRect.size);
            var painter = context.painter2D;
            painter.strokeColor = _strokeColor;
            painter.lineWidth = _strokeWidth * scale;
            painter.lineJoin = LineJoin.Miter;
            painter.miterLimit = SvgDefaultMiterLimit;
            ProgressArrowGlyphPath.Trace(painter, scale);
            painter.Stroke();
        }
    }
}
