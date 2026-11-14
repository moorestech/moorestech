using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Theme.Mesh
{
    /// <summary>
    /// Painter2D に無い square の線端を、端を線幅の半分だけ線の向きへ延ばして作る（SVG stroke-linecap: square の写し）
    /// Makes the square line cap Painter2D lacks by extending each end by half the stroke width along the line (port of SVG stroke-linecap: square)
    /// </summary>
    public static class Painter2DSquareCap
    {
        public static void AddSegment(Painter2D painter, Vector2 from, Vector2 to, float halfWidth)
        {
            painter.MoveTo(Extend(from, to, halfWidth));
            painter.LineTo(Extend(to, from, halfWidth));
        }

        // end を from から離れる向きへ length だけ延ばす
        // Extends end by length, away from from
        public static Vector2 Extend(Vector2 end, Vector2 from, float length)
        {
            return end + (end - from).normalized * length;
        }
    }
}
