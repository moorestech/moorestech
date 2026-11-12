using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Skit.Parts.Choices
{
    /// <summary>
    /// 選択肢の両端の菱形（Web ChoiceMarkerIcon。viewBox 12 の外側と内側の 2 つの菱形を線で描く）
    /// The diamond at each end of a choice (Web ChoiceMarkerIcon; strokes the outer and inner diamonds of a viewBox-12 icon)
    /// </summary>
    public sealed class SkitChoiceMarker : VisualElement
    {
        private const float ViewBoxSize = 12f;

        private Color _color;
        private float _stroke;
        private bool _hasTokens;

        public SkitChoiceMarker(string positionClassName)
        {
            AddToClassList("skit-choice__marker");
            AddToClassList(positionClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyleResolved());
        }

        // ホバーで色が変わると customStyle が解き直されるので、そのたびに描き直す
        // Hover changes re-resolve the custom style, so redraw each time
        private void OnCustomStyleResolved()
        {
            var hasColor = UssCustomStyleReader.TryReadColor(customStyle, "--skit-choice-marker-color", out _color);
            var hasStroke = UssCustomStyleReader.TryReadNumber(customStyle, "--skit-choice-marker-stroke", out _stroke);
            _hasTokens = hasColor && hasStroke;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            // silent-ok: トークンが読めない間は描かない / UssCustomStyleReader が読めなかったトークン名を LogError で出す
            if (!_hasTokens) return;

            // M6 0.5 L11.5 6 L6 11.5 L0.5 6 Z と M6 3.5 L8.5 6 L6 8.5 L3.5 6 Z
            // M6 0.5 L11.5 6 L6 11.5 L0.5 6 Z and M6 3.5 L8.5 6 L6 8.5 L3.5 6 Z
            var scale = contentRect.width / ViewBoxSize;
            var painter = context.painter2D;
            painter.lineWidth = _stroke * scale;
            painter.lineJoin = LineJoin.Miter;
            painter.strokeColor = _color;
            StrokeDiamond(5.5f);
            StrokeDiamond(2.5f);

            #region Internal

            void StrokeDiamond(float halfSize)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(6f, 6f - halfSize) * scale);
                painter.LineTo(new Vector2(6f + halfSize, 6f) * scale);
                painter.LineTo(new Vector2(6f, 6f + halfSize) * scale);
                painter.LineTo(new Vector2(6f - halfSize, 6f) * scale);
                painter.ClosePath();
                painter.Stroke();
            }

            #endregion
        }
    }
}
