using Client.Common;
using UnityEngine;

namespace Client.Game.InGame.Interact.Selection
{
    /// <summary>
    ///     インタラクト距離内の当たり判定を集める。候補選定と到達判定が同じ距離・レイヤを使うための一元化
    ///     Collects colliders within the interact distance so selection and reach checks share one distance and layer set
    /// </summary>
    public static class InteractOverlap
    {
        public const int InitialBufferSize = 64;

        public static readonly int InteractLayerMask = LayerConst.BlockOnlyLayerMask | LayerConst.MapObjectOnlyLayerMask;

        // 飽和したまま返すと取りこぼした候補次第で結果が変わるため、バッファを倍にして採り直す
        // A saturated buffer would make the result depend on which colliders were dropped, so it is doubled and re-queried
        public static int OverlapNearby(Vector3 center, ref Collider[] buffer)
        {
            while (true)
            {
                var count = Physics.OverlapSphereNonAlloc(center, InteractTargetSelector.InteractDistance, buffer, InteractLayerMask);
                if (count < buffer.Length) return count;

                buffer = new Collider[buffer.Length * 2];
            }
        }
    }
}
