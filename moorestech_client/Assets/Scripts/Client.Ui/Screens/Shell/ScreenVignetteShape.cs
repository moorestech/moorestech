using UnityEngine;

namespace Client.Ui.Screens.Shell
{
    /// <summary>
    /// CSS の radial-gradient(ellipse W H at X Y, clear S, edge 100%) の縁色の重み（0〜1）を要素内の点ごとに返す
    /// Returns the edge-color weight (0 to 1) of CSS radial-gradient(ellipse W H at X Y, clear S, edge 100%) at each point of the element
    /// </summary>
    /// <remarks>
    /// 楕円の半径は要素の幅・高さ × 比、中心は幅・高さ × 位置の比。停止点より内は 0、楕円上と外は 1、その間は線形（色は乗算済みで補間されるので透明の色成分は効かない）
    /// The ellipse radii are the element width and height times the ratios and the center is width and height times the position ratios; zero inside the stop, one on and beyond the ellipse, linear between (colors interpolate premultiplied, so the clear stop's RGB has no effect)
    /// </remarks>
    internal readonly struct ScreenVignetteShape
    {
        private readonly float _widthRatio;
        private readonly float _heightRatio;
        private readonly float _centerX;
        private readonly float _centerY;
        private readonly float _clearStop;

        public ScreenVignetteShape(float widthRatio, float heightRatio, float centerX, float centerY, float clearStop)
        {
            _widthRatio = widthRatio;
            _heightRatio = heightRatio;
            _centerX = centerX;
            _centerY = centerY;
            _clearStop = clearStop;
        }

        public float EdgeWeightAt(float x, float y, float width, float height)
        {
            // 楕円で正規化した中心からの距離 t（楕円上で 1）
            // Distance from the center normalized by the ellipse (one on the ellipse)
            var dx = (x - width * _centerX) / (width * _widthRatio);
            var dy = (y - height * _centerY) / (height * _heightRatio);
            var t = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01((t - _clearStop) / (1f - _clearStop));
        }
    }
}
