using Client.Common;
using UnityEngine;

namespace Client.Game.InGame.Interact.Selection
{
    /// <summary>
    ///     候補選定の近傍探索と、インタラクト距離・レイヤの正本
    ///     The nearby query for candidate selection and the single source of the interact distance and layer set
    /// </summary>
    public static class InteractOverlap
    {
        // Fで開ける距離と開いたインベントリが閉じる距離の唯一の正本。自機から対象表面までで測る
        // The single source for both the F-open distance and the open inventory's close distance, measured from the player to the target surface
        public const float InteractDistance = 2f;

        internal const int InitialBufferSize = 64;

        internal static readonly int InteractLayerMask = LayerConst.BlockOnlyLayerMask | LayerConst.MapObjectOnlyLayerMask;

        // 飽和したまま返すと取りこぼした候補次第で結果が変わるため、バッファを倍にして採り直す
        // A saturated buffer would make the result depend on which colliders were dropped, so it is doubled and re-queried
        internal static int OverlapNearby(Vector3 center, ref Collider[] buffer)
        {
            while (true)
            {
                var count = Physics.OverlapSphereNonAlloc(center, InteractDistance, buffer, InteractLayerMask);
                if (count < buffer.Length) return count;

                buffer = new Collider[buffer.Length * 2];
            }
        }
    }
}
