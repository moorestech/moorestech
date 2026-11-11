// S/Client.Ui/Screens/Skit/Parts/Toolbar/SkitToolGlyphPaths.cs
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Skit.Parts.Toolbar
{
    /// <summary>
    /// Web の SVG（viewBox 24）の path を Painter2D の線へ写す。座標は Web の d 属性のまま
    /// Copies the Web SVG paths (viewBox 24) onto Painter2D strokes; coordinates are the Web d attributes as is
    /// </summary>
    public static class SkitToolGlyphPaths
    {
        public const float ViewBoxSize = 24f;

        public static void Stroke(Painter2D painter, SkitToolGlyphShape shape, float scale, Vector2 offset)
        {
            switch (shape)
            {
                case SkitToolGlyphShape.Auto:
                    StrokeCircle(12f, 12f, 9f);
                    StrokePolyline(true, 10f, 8f, 16f, 12f, 10f, 16f);
                    return;
                case SkitToolGlyphShape.Skip:
                    StrokePolyline(false, 4f, 6f, 10f, 12f, 4f, 18f);
                    StrokePolyline(false, 11f, 6f, 17f, 12f, 11f, 18f);
                    StrokePolyline(false, 20f, 6f, 20f, 18f);
                    return;
                case SkitToolGlyphShape.HideUi:
                    StrokeEye();
                    StrokeCircle(12f, 12f, 3f);
                    StrokePolyline(false, 4f, 4f, 20f, 20f);
                    return;
                case SkitToolGlyphShape.ShowUi:
                    StrokeEye();
                    StrokeCircle(12f, 12f, 3f);
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(shape), shape, null);
            }

            #region Internal

            Vector2 At(float x, float y)
            {
                return new Vector2(x * scale, y * scale) + offset;
            }

            void StrokeCircle(float centerX, float centerY, float radius)
            {
                painter.BeginPath();
                painter.Arc(At(centerX, centerY), radius * scale, Angle.Degrees(0f), Angle.Degrees(360f));
                painter.Stroke();
            }

            // 座標の組を順に結ぶ。closed なら始点へ戻して閉じる（SVG の Z）
            // Joins coordinate pairs in order; when closed, return to the start (SVG Z)
            void StrokePolyline(bool closed, params float[] points)
            {
                painter.BeginPath();
                painter.MoveTo(At(points[0], points[1]));
                for (var i = 2; i < points.Length; i += 2) painter.LineTo(At(points[i], points[i + 1]));
                if (closed) painter.ClosePath();
                painter.Stroke();
            }

            // M2.5 12 C6 6.5 9 5 12 5 C15 5 18 6.5 21.5 12 C18 17.5 15 19 12 19 C9 19 6 17.5 2.5 12 Z
            void StrokeEye()
            {
                painter.BeginPath();
                painter.MoveTo(At(2.5f, 12f));
                painter.BezierCurveTo(At(6f, 6.5f), At(9f, 5f), At(12f, 5f));
                painter.BezierCurveTo(At(15f, 5f), At(18f, 6.5f), At(21.5f, 12f));
                painter.BezierCurveTo(At(18f, 17.5f), At(15f, 19f), At(12f, 19f));
                painter.BezierCurveTo(At(9f, 19f), At(6f, 17.5f), At(2.5f, 12f));
                painter.ClosePath();
                painter.Stroke();
            }

            #endregion
        }
    }
}
