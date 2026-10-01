using System;
using UnityEngine;

namespace Client.Game.InGame.Interact.Selection
{
    /// <summary>
    ///     開いた対象が届く範囲かを、その対象だけを測って答える
    ///     Answers whether an opened target is within reach by measuring that target alone
    /// </summary>
    public class InteractReachQuery
    {
        private IInteractable _measuredTarget;
        private Collider[] _targetColliders = Array.Empty<Collider>();

        public InteractReachResult QueryReach(IInteractable target, Vector3 playerPosition)
        {
            // 破棄済みの実体はinterface型のnull比較を素通りするので、Unityのfake-null比較で先に落とす
            // A destroyed instance slips through an interface-typed null check, so Unity's fake-null compare drops it first
            if (target == null) return InteractReachResult.TargetDestroyed;
            if (target is UnityEngine.Object unityTarget && unityTarget == null) return InteractReachResult.TargetDestroyed;
            if (target.GameObject == null) return InteractReachResult.TargetDestroyed;
            if (!target.IsInteractAvailable) return InteractReachResult.NotInteractable;

            var colliders = ResolveTargetColliders(target);
            for (var index = 0; index < colliders.Length; index++)
            {
                var collider = colliders[index];
                if (collider == null) continue;
                if ((InteractOverlap.InteractLayerMask & (1 << collider.gameObject.layer)) == 0) continue;

                // 非凸MeshColliderでも安全な境界上の最近点で測る（候補選定と同じ測り方）
                // Measured at the closest point on the bounds, which is safe even for a non-convex MeshCollider, exactly as selection measures it
                if (Vector3.Distance(collider.ClosestPointOnBounds(playerPosition), playerPosition) <= InteractOverlap.InteractDistance) return InteractReachResult.Reachable;
            }

            return InteractReachResult.OutOfRange;

            #region Internal

            // 当たり判定子は作り直されない限り変わらないので、対象の実体が変わった時だけ採り直す
            // Collider children do not change unless the view is rebuilt, so they are re-collected only when the target instance changes
            Collider[] ResolveTargetColliders(IInteractable measuredTarget)
            {
                if (ReferenceEquals(_measuredTarget, measuredTarget)) return _targetColliders;

                _measuredTarget = measuredTarget;
                // スキット中に非アクティブ化された当たり判定子も測る（表示が隠れただけで離れてはいない）
                // Colliders deactivated during a skit are measured too, since the view is only hidden and the player has not moved away
                _targetColliders = measuredTarget.GameObject.GetComponentsInChildren<Collider>(true);
                return _targetColliders;
            }

            #endregion
        }
    }
}
