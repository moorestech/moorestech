using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Gauges
{
    /// <summary>
    /// 矢印の path を 1 色で塗る層（Web の .track と .fill の path）。色は層のクラスが --progress-arrow-glyph-layer-face で渡す
    /// Layer filling the arrow path with one color (the Web .track and .fill paths); the layer class passes it as --progress-arrow-glyph-layer-face
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

            // 層の箱は viewBox に当たるので、その倍率で path を拡げて塗る
            // The layer box is the viewBox, so scale the path by its ratio and fill it
            var painter = context.painter2D;
            painter.fillColor = _faceColor;
            ProgressArrowGlyphPath.Trace(painter, ProgressArrowGlyphPath.ScaleOf(contentRect.size));
            painter.Fill(FillRule.NonZero);
        }
    }
}
