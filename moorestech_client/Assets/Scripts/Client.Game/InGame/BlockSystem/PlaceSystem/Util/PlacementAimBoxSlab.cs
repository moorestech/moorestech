using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// 照準レイと軸平行な箱の交差（スラブ法）と、軸から面への対応
    /// Ray vs axis-aligned box intersection (slab method) and axis-to-face mapping
    /// </summary>
    internal static class PlacementAimBoxSlab
    {
        // 同値のときに優先する軸の順（Y → X → Z）
        // Axis priority on ties (Y -> X -> Z)
        private static readonly int[] AxisPriority = { 1, 0, 2 };

        public static bool TryIntersect(Ray ray, Vector3 boxMin, Vector3 boxMax, out float tEnter, out int enterAxis, out float tExit, out int exitAxis)
        {
            tEnter = float.NegativeInfinity;
            tExit = float.PositiveInfinity;
            enterAxis = AxisPriority[0];
            exitAxis = AxisPriority[0];
            var origin = ray.origin;
            var direction = ray.direction;

            foreach (var axis in AxisPriority)
            {
                // 進まない軸は始点がスラブ内にあるときだけ区間を制限しない
                // An axis without motion only passes when the origin lies inside its slab
                if (direction[axis] == 0f)
                {
                    if (origin[axis] < boxMin[axis] || origin[axis] > boxMax[axis]) return false;
                    continue;
                }

                // 近い境界と遠い境界までの距離を求め、優先順で厳密に更新する
                // Distances to the near and far planes, updated strictly in priority order
                var t1 = (boxMin[axis] - origin[axis]) / direction[axis];
                var t2 = (boxMax[axis] - origin[axis]) / direction[axis];
                var near = Mathf.Min(t1, t2);
                var far = Mathf.Max(t1, t2);
                if (near > tEnter)
                {
                    tEnter = near;
                    enterAxis = axis;
                }
                if (far < tExit)
                {
                    tExit = far;
                    exitAxis = axis;
                }
            }

            return tEnter <= tExit && tExit >= 0f;
        }

        public static int NearestAxis(Vector3 tMax)
        {
            // 最小の値を持つ軸を、同値なら優先順の先を選ぶ
            // Pick the axis with the smallest value, preferring the earlier priority on ties
            var best = AxisPriority[0];
            foreach (var axis in AxisPriority)
            {
                if (tMax[axis] < tMax[best]) best = axis;
            }
            return best;
        }

        public static PreviewSurfaceType EnterFace(int axis, bool positiveStep)
        {
            // 正の向きに入るなら原点側の面、負の向きなら奥側の面
            // Entering along + hits the origin-side face, along - the far face
            return axis switch
            {
                0 => positiveStep ? PreviewSurfaceType.YZ_Origin : PreviewSurfaceType.YZ_X,
                1 => positiveStep ? PreviewSurfaceType.XZ_Origin : PreviewSurfaceType.XZ_Y,
                _ => positiveStep ? PreviewSurfaceType.YX_Origin : PreviewSurfaceType.YX_Z,
            };
        }

        public static PreviewSurfaceType ExitFace(int axis, bool positiveStep)
        {
            // 出る面は逆向きに入る面と同じ
            // The exit face equals the face entered from the opposite direction
            return EnterFace(axis, !positiveStep);
        }
    }
}
