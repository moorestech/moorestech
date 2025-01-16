using UnityEngine;
namespace Game.Train.Utility
{ 
    //途中/////////////
    //途中/////////////
    //途中/////////////
    //途中/////////////
    //途中/////////////
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
    }
}