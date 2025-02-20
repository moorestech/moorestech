using Game.Train.RailGraph;
using UnityEngine;
namespace Game.Train.Utility
{ 
    public static class BezierUtility
    {
        /// <summary>
        /// t を与えたときのベジェ曲線上の座標を返す関数（3次ベジェ曲線）
        /// </summary>
        /// <param name="p0">アンカーポイント1</param>
        /// <param name="p1">p0 の制御点</param>
        /// <param name="p2">p3 の制御点</param>
        /// <param name="p3">アンカーポイント2</param>
        /// <param name="t">パラメータ t（0 <= t <= 1）</param>
        /// <returns>ベジェ曲線上の座標</returns>

        //ベジェ曲線の長さはワールド座標と同じスケール
        //ただしRailNodeの距離はint(固定小数点を想定)であるため定数倍の差がある
        //そのスケールはとりあえずここに書いておく
        public const float RAIL_LENGTH_SCALE = 1024.0f;
        public static Vector3 GetBezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector3 point = uuu * p0;             // (1 - t)^3 * p0
            point += 3f * uu * t * p1;            // 3(1 - t)^2 * t * p1
            point += 3f * u * tt * p2;            // 3(1 - t) * t^2 * p2
            point += ttt * p3;                    // t^3 * p3

            return point;
        }

        /// <summary>
        /// 3次ベジェ曲線の概算距離を返す関数
        /// </summary>
        /// <param name="p0">アンカーポイント1</param>
        /// <param name="p1">p0 の制御点</param>
        /// <param name="p2">p3 の制御点</param>
        /// <param name="p3">アンカーポイント2</param>
        /// <param name="samples">サンプリング数（多いほど精度が高くなる）</param>
        /// <returns>ベジェ曲線の概算距離</returns>
        public static float GetBezierCurveLength(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int samples = 512)
        {
            if (p0 == p3) 
                return 0;
            float length = 0f;
            Vector3 previousPoint = GetBezierPoint(p0, p1, p2, p3, 0f);

            for (int i = 1; i <= samples; i++)
            {
                float t = (float)i / samples;
                Vector3 currentPoint = GetBezierPoint(p0, p1, p2, p3, t);
                length += Vector3.Distance(previousPoint, currentPoint);
                previousPoint = currentPoint;
            }

            return length;
        }

        // RailControlPointの座標の入力
        // floatが非常に大きい場合でおこる誤差をちゃんと考慮したver
        public static float GetBezierCurveLength(RailControlPoint cp0, RailControlPoint cp1, int samples = 512)
        {
            var p0 = cp0.OriginalPosition;
            var p1 = cp0.ControlPointPosition;
            var p2 = cp1.ControlPointPosition;
            var p3 = cp1.OriginalPosition;
            p3 -= p0;
            p0 -= p0;
            p1 += p0;
            p2 += p3;
            return GetBezierCurveLength(p0, p1, p2, p3, samples);
        }
    }
}