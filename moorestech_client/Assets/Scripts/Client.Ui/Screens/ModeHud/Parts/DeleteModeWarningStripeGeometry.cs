using System.Collections.Generic;
using UnityEngine;

namespace Client.Ui.Screens.ModeHud.Parts
{
    /// <summary>
    /// 勾配の 1 色目の帯を凸多角形の列にする
    /// Turns first-colour gradient stripes into convex polygons
    /// </summary>
    public static class DeleteModeWarningStripeGeometry
    {
        public static IReadOnlyList<IReadOnlyList<Vector2>> FirstColorPolygons(Vector2 size, float angleDegrees, float stripeSize)
        {
            // CSS の勾配線: 方向 (sin a, -cos a)（y 下向き）、長さ |w sin a| + |h cos a|、要素の中心を通る
            // CSS gradient line: direction (sin a, -cos a) with y down, length |w sin a| + |h cos a|, through the element centre
            var radians = angleDegrees * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians));
            var length = Mathf.Abs(size.x * Mathf.Sin(radians)) + Mathf.Abs(size.y * Mathf.Cos(radians));
            var start = size / 2f - direction * (length / 2f);
            var rect = new List<Vector2> { Vector2.zero, new(size.x, 0f), size, new(0f, size.y) };

            // 周期 2s ごとの帯を切り出す
            // Cut a slab every 2s period
            var polygons = new List<IReadOnlyList<Vector2>>();
            for (var from = 0f; from < length; from += stripeSize * 2f)
            {
                var slab = ClipByHalfPlane(rect, start, direction, from);
                slab = ClipByHalfPlane(slab, start, -direction, -(from + stripeSize));
                if (3 <= slab.Count) polygons.Add(slab);
            }
            return polygons;

            #region Internal

            // dot(p - origin, normal) >= offset の側を残す（Sutherland–Hodgman の 1 辺）
            // Keep the side where dot(p - origin, normal) >= offset (one edge of Sutherland–Hodgman)
            List<Vector2> ClipByHalfPlane(List<Vector2> polygon, Vector2 origin, Vector2 normal, float offset)
            {
                var clipped = new List<Vector2>();
                for (var i = 0; i < polygon.Count; i++)
                {
                    var current = polygon[i];
                    var next = polygon[(i + 1) % polygon.Count];
                    var currentDistance = Vector2.Dot(current - origin, normal) - offset;
                    var nextDistance = Vector2.Dot(next - origin, normal) - offset;
                    if (0f <= currentDistance) clipped.Add(current);
                    if ((0f <= currentDistance) != (0f <= nextDistance)) clipped.Add(Vector2.Lerp(current, next, currentDistance / (currentDistance - nextDistance)));
                }
                return clipped;
            }

            #endregion
        }
    }
}
