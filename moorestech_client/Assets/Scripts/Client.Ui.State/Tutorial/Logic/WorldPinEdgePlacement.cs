using System;

namespace Client.Ui.State.Tutorial.Logic
{
    /// <summary>
    /// 画面外ピンの矢印を画面端へクランプする位置と角度。Web の WorldPinOverlay の EdgeArrow の写し
    /// Clamps an off-screen pin's arrow to the screen edge; a copy of Web's WorldPinOverlay EdgeArrow
    /// </summary>
    public static class WorldPinEdgePlacement
    {
        public static bool TryPlace(float width, float height, float edgeMargin, float directionX, float directionY, out WorldPinEdgeSpot spot)
        {
            spot = default;
            // silent-ok: 層の寸法が 0 なのはレイアウト前の 1 フレームだけで、次の GeometryChangedEvent で置き直す / L1 WorldPinOverlayTest が位置を見る
            if (width == 0 || height == 0) return false;
            // 方向を画面端の余白の内側へクランプする
            // Clamp the direction to inside the screen edge margin
            var maxX = width / 2f - edgeMargin;
            var maxY = height / 2f - edgeMargin;
            var scaleX = 0.001f < Math.Abs(directionX) ? maxX / Math.Abs(directionX) : float.MaxValue;
            var scaleY = 0.001f < Math.Abs(directionY) ? maxY / Math.Abs(directionY) : float.MaxValue;
            var scale = Math.Min(scaleX, scaleY);
            var angle = (float)(Math.Atan2(directionY, directionX) * 180.0 / Math.PI);
            spot = new WorldPinEdgeSpot(width / 2f + directionX * scale, height / 2f + directionY * scale, angle);
            return true;
        }
    }

    public readonly struct WorldPinEdgeSpot
    {
        public readonly float Left;
        public readonly float Top;
        public readonly float AngleDegrees;

        public WorldPinEdgeSpot(float left, float top, float angleDegrees)
        {
            Left = left;
            Top = top;
            AngleDegrees = angleDegrees;
        }
    }
}
