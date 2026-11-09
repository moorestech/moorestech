using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Gauges
{
    /// <summary>
    /// 矢印の drop-shadow の層。線の外縁を影の形として、ずらしてにじませた輪のメッシュを描く（設計書 A-88）
    /// Drop-shadow layer of the arrow; draws the shifted, blurred ring mesh using the stroke's outer edge as the shadow shape (design A-88)
    /// </summary>
    internal sealed class ProgressArrowGlyphShadow : VisualElement
    {
        private const string ShadowColorProperty = "--progress-arrow-glyph-layer-shadow";
        private const string OffsetYProperty = "--progress-arrow-glyph-layer-shadow-offset-y";
        private const string BlurProperty = "--progress-arrow-glyph-layer-shadow-blur";
        private const string StrokeWidthProperty = "--progress-arrow-glyph-layer-stroke-width";

        private readonly Vertex[] _vertices = new Vertex[ProgressArrowGlyphShadowMesh.VertexCount];
        private readonly ushort[] _indices = new ushort[ProgressArrowGlyphShadowMesh.IndexCount];
        private Color _shadowColor;
        private float _offsetY;
        private float _blur;
        private float _strokeWidth;
        private bool _hasStyle;

        public ProgressArrowGlyphShadow()
        {
            AddToClassList("progress-arrow-glyph__shadow");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += DrawShadow;
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyleResolved());
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        private void OnCustomStyleResolved()
        {
            var hasColor = UssCustomStyleReader.TryReadColor(customStyle, ShadowColorProperty, out _shadowColor);
            var hasOffset = UssCustomStyleReader.TryReadPixels(customStyle, OffsetYProperty, out _offsetY);
            var hasBlur = UssCustomStyleReader.TryReadPixels(customStyle, BlurProperty, out _blur);
            var hasWidth = UssCustomStyleReader.TryReadPixels(customStyle, StrokeWidthProperty, out _strokeWidth);
            _hasStyle = hasColor && hasOffset && hasBlur && hasWidth;

            // ぼかし 0 は σ=0 で輪が潰れるので描かない（Web の値は 1px。0 にした USS の誤りを知らせる）
            // A zero blur collapses the rings at σ=0, so skip drawing (the Web value is 1px; report the USS mistake)
            if (_hasStyle && _blur <= 0f)
            {
                Debug.LogError($"[ProgressArrowGlyphShadow] {BlurProperty} must be positive but was {_blur}px; the shadow is not drawn");
                _hasStyle = false;
            }
            MarkDirtyRepaint();
        }

        private void DrawShadow(MeshGenerationContext context)
        {
            // silent-ok: 値の解決前と寸法 0（非表示・配置前）は描かない / 値は UssCustomStyleReader と上の LogError が知らせ、寸法のつまみの誤りは L1 の寸法テストと並べ比べが捕まえる
            // silent-ok: draw nothing before values resolve or at size 0 (hidden, pre-layout) / UssCustomStyleReader and the LogError above report values; L1 size tests and the comparison catch wrong size knobs
            var scale = ProgressArrowGlyphPath.ScaleOf(contentRect.size);
            if (!_hasStyle || scale <= 0f) return;

            // 影の形は線の外縁（線幅の半分だけ外）。ずれとぼかしは CSS の px のままで viewBox の倍率を掛けない
            // The shadow shape is the stroke's outer edge (half the width outside); offset and blur stay in CSS px, unscaled by the viewBox
            ProgressArrowGlyphShadowMesh.Build(scale, _strokeWidth * scale / 2f, _offsetY, _blur, _shadowColor, _vertices, _indices);
            var mesh = context.Allocate(_vertices.Length, _indices.Length);
            mesh.SetAllVertices(_vertices);
            mesh.SetAllIndices(_indices);
        }
    }
}
