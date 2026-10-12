using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using UnityEngine;

namespace Client.Game.InGame.Control
{
    /// <summary>
    ///     削除ツールの照準解決。1本のレイで対象を選ぶ
    ///     Delete-tool aim resolution: one ray over both layers picks a target
    /// </summary>
    public static class DeleteTargetRaycaster
    {
        private static readonly List<DeleteTargetHit> HitCandidates = new();

        public static DeleteAimResult AimAt(DeleteAimFilter filter)
        {
            var mask = LayerConst.BlockOnlyLayerMask | LayerConst.ConnectionLineOnlyLayerMask;
            var hitCount = BlockClickDetectUtil.RaycastAimAll(mask, BlockClickDetectUtil.AimRayDistance, out var hits);

            // 設置ゴーストは貫通し、それ以外のヒットを候補へ積む
            // Pass through placement ghosts and collect every other hit as a candidate
            HitCandidates.Clear();
            for (var i = 0; i < hitCount; i++)
            {
                var collider = hits[i].collider;
                if (collider.GetComponentInParent<BlockPreviewObject>() != null) continue;
                HitCandidates.Add(new DeleteTargetHit(hits[i].distance, ResolveTarget(collider)));
            }

            return DeleteTargetHitSelector.Select(HitCandidates, filter);

            #region Internal

            // 接続線のコライダーは線本体の子、ブロック・レール・車両は従来どおり自身か子に対象を持つ
            // Connection-line colliders sit under the line, while blocks/rails/cars keep the target on self or children as before
            static IDeleteTarget ResolveTarget(Collider collider)
            {
                if (collider.gameObject.layer == LayerConst.ConnectionLineLayer) return ConnectionLineDeleteTarget.FromCollider(collider);
                return collider.gameObject.GetComponentInChildren<IDeleteTarget>();
            }

            #endregion
        }
    }
}
