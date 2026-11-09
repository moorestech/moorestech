using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Tutorial.WorldPin
{
    /// <summary>
    /// ワールドピンのマーカーと画面端の矢印の形（Web WorldPinOverlay の svg path。viewBox 24）
    /// World pin marker and edge arrow shapes (Web WorldPinOverlay svg paths; viewBox 24)
    /// </summary>
    public static class WorldPinGlyphs
    {
        private const float ViewBox = 24f;
        private const float StrokeWidth = 1.5f;
        private static readonly Vector2[] ArrowPath = { new(2f, 8f), new(13f, 8f), new(13f, 3f), new(22f, 12f), new(13f, 21f), new(13f, 16f), new(2f, 16f) };

        public static void DrawMarker(Painter2D painter, float size, Color face, Color stroke)
        {
            // M12 22 L5 10 A8 8 0 1 1 19 10 Z。弧の中心は (12, 10-√15)、大円弧を時計回り
            // M12 22 L5 10 A8 8 0 1 1 19 10 Z; the arc center is (12, 10-√15), the large arc runs clockwise
            var unit = size / ViewBox;
            var centerY = 10f - Mathf.Sqrt(15f);
            var center = new Vector2(12f, centerY) * unit;
            var start = Mathf.Atan2(10f - centerY, 5f - 12f) * Mathf.Rad2Deg;
            var end = Mathf.Atan2(10f - centerY, 19f - 12f) * Mathf.Rad2Deg + 360f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(12f, 22f) * unit);
            painter.LineTo(new Vector2(5f, 10f) * unit);
            painter.Arc(center, 8f * unit, Angle.Degrees(start), Angle.Degrees(end), ArcDirection.Clockwise);
            painter.ClosePath();
            painter.fillColor = face;
            painter.Fill();
            // stroke-width は viewBox 単位なので unit を掛ける
            // stroke-width is in viewBox units, so scale it by unit
            painter.strokeColor = stroke;
            painter.lineWidth = StrokeWidth * unit;
            painter.Stroke();
        }

        public static void DrawArrow(Painter2D painter, float size, Color fill, Color stroke, Color shadow)
        {
            // 影は blur を持てないので 2px 下の写しで近似し（LX-36）、縁を塗りの下に描く（paint-order: stroke fill）
            // Approximate the blurred shadow with a copy 2px down (LX-36), then paint the stroke under the fill (paint-order: stroke fill)
            var unit = size / ViewBox;
            Trace(painter, unit, new Vector2(0f, 2f)); // look-exception: LX-36
            painter.fillColor = shadow;
            painter.Fill();
            Trace(painter, unit, Vector2.zero);
            painter.strokeColor = stroke;
            painter.lineWidth = StrokeWidth * unit;
            painter.lineJoin = LineJoin.Round;
            painter.Stroke();
            painter.fillColor = fill;
            painter.Fill();

            #region Internal

            void Trace(Painter2D target, float scale, Vector2 offset)
            {
                target.BeginPath();
                target.MoveTo(ArrowPath[0] * scale + offset);
                for (var i = 1; i < ArrowPath.Length; i++) target.LineTo(ArrowPath[i] * scale + offset);
                target.ClosePath();
            }

            #endregion
        }
    }
}
