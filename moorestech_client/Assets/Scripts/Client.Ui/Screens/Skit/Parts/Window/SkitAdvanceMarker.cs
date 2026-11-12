using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Skit.Parts.Window
{
    /// <summary>
    /// 送り待ちの二重の山形（Web AdvanceMarkerIcon。viewBox 24・角の端）
    /// The double chevron that waits for advance (Web AdvanceMarkerIcon; viewBox 24, square caps)
    /// </summary>
    public sealed class SkitAdvanceMarker : VisualElement
    {
        private const float ViewBoxSize = 24f;

        private Color _color;
        private float _stroke;
        private bool _hasTokens;

        public SkitAdvanceMarker()
        {
            name = "skit-advance-marker";
            AddToClassList("skit-advance-marker");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyleResolved());
        }

        private void OnCustomStyleResolved()
        {
            var hasColor = UssCustomStyleReader.TryReadColor(customStyle, "--skit-advance-marker-color", out _color);
            var hasStroke = UssCustomStyleReader.TryReadNumber(customStyle, "--skit-advance-marker-stroke", out _stroke);
            _hasTokens = hasColor && hasStroke;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            // silent-ok: トークンが読めない間は描かない / UssCustomStyleReader が読めなかったトークン名を LogError で出す
            if (!_hasTokens) return;

            // M5 6 L12 13 L19 6 と M5 12 L12 19 L19 12
            // M5 6 L12 13 L19 6 and M5 12 L12 19 L19 12
            var scale = contentRect.width / ViewBoxSize;
            var painter = context.painter2D;
            painter.lineWidth = _stroke * scale;
            painter.lineCap = LineCap.Butt;
            painter.lineJoin = LineJoin.Miter;
            painter.strokeColor = _color;
            StrokeChevron(6f);
            StrokeChevron(12f);

            #region Internal

            void StrokeChevron(float top)
            {
                // Painter2D に角の端（square）が無いので、両端を線幅の半分だけ線の向きへ延ばした Butt で同じ形にする
                // Painter2D has no square cap, so extend both ends by half the stroke along the segment and draw Butt for the same shape
                var vertex = new Vector2(12f, top + 7f);
                var halfStroke = _stroke * 0.5f;
                painter.BeginPath();
                painter.MoveTo(ExtendedEnd(new Vector2(5f, top), vertex, halfStroke) * scale);
                painter.LineTo(vertex * scale);
                painter.LineTo(ExtendedEnd(new Vector2(19f, top), vertex, halfStroke) * scale);
                painter.Stroke();
            }

            Vector2 ExtendedEnd(Vector2 end, Vector2 from, float length)
            {
                return end + (end - from).normalized * length;
            }

            #endregion
        }
    }
}
