using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Gauges
{
    /// <summary>
    /// 矢印 path を 1 色で塗る層（色は層クラスが渡す）
    /// Layer filling the arrow path with one color (the layer class passes it)
    /// </summary>
    internal sealed class ProgressArrowGlyphFace : VisualElement
    {
        private const string FaceColorProperty = "--progress-arrow-glyph-layer-face";

        private Color _faceColor;
        private bool _hasStyle;

        public ProgressArrowGlyphFace(string layerClassName)
        {
            AddToClassList(layerClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += DrawFace;
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyleResolved());
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        private void OnCustomStyleResolved()
        {
            _hasStyle = UssCustomStyleReader.TryReadColor(customStyle, FaceColorProperty, out _faceColor);
            MarkDirtyRepaint();
        }

        private void DrawFace(MeshGenerationContext context)
        {
            // silent-ok: 色の解決前は描かない / 解決できなければ UssCustomStyleReader が LogError を出す
            // silent-ok: draw nothing until the color resolves / UssCustomStyleReader logs an error if it never does
            if (!_hasStyle) return;

            // 箱の倍率で path を拡げて塗る
            // Scale the path by the box ratio and fill
            var painter = context.painter2D;
            painter.fillColor = _faceColor;
            ProgressArrowGlyphPath.Trace(painter, ProgressArrowGlyphPath.ScaleOf(contentRect.size));
            painter.Fill(FillRule.NonZero);
        }
    }
}
