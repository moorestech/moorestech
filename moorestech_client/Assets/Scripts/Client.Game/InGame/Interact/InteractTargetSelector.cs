using Client.Common;
using Client.Game.InGame.Control;
using Client.Game.InGame.Player;
using UnityEngine;

namespace Client.Game.InGame.Interact
{
    /// <summary>
    ///     インタラクト対象を常に1件だけ選ぶ。照準レイのヒットを優先し、無ければ半径2m内で視線角度が最小のもの（ADR 0046）
    ///     Picks exactly one interactable: the aim-ray hit first, else the smallest view angle within 2m (ADR 0046)
    /// </summary>
    public class InteractTargetSelector
    {
        public const float InteractDistance = 2f;

        private const int OverlapBufferSize = 64;

        private static readonly int InteractLayerMask = LayerConst.BlockOnlyLayerMask | LayerConst.MapObjectOnlyLayerMask;

        private readonly Collider[] _overlapBuffer = new Collider[OverlapBufferSize];

        public virtual IInteractable Select()
        {
            var camera = Camera.main;
            if (camera == null) return null;
            if (UiPointerHitTest.IsPointerOverAnyUi()) return null;

            var playerPosition = PlayerSystemContainer.Instance.PlayerObjectController.Position;
            return SelectByAimRay() ?? SelectNearbyByViewAngle();

            #region Internal

            IInteractable SelectByAimRay()
            {
                // 手順はBlockClickDetectUtilに集約済み
                // Ray creation, distance sort and ghost penetration are centralized in BlockClickDetectUtil
                if (!BlockClickDetectUtil.TryGetFrontmostSolidHit(InteractLayerMask, out var hit)) return null;

                // 届かなければ近傍探索へフォールバック
                // If the frontmost solid is not a reachable target, fall through to the nearby search
                if (!InteractableResolver.TryResolve(hit.collider, out var interactable)) return null;
                return IsWithinReach(interactable) ? interactable : null;
            }

            IInteractable SelectNearbyByViewAngle()
            {
                var hitCount = Physics.OverlapSphereNonAlloc(playerPosition, InteractDistance, _overlapBuffer, InteractLayerMask);
                var forward = camera.transform.forward;
                IInteractable best = null;
                var bestAngle = float.PositiveInfinity;
                var bestSqrDistance = float.PositiveInfinity;

                for (var index = 0; index < hitCount; index++)
                {
                    if (!InteractableResolver.TryResolve(_overlapBuffer[index], out var candidate)) continue;

                    var toCandidate = candidate.GameObject.transform.position - playerPosition;
                    var angle = Vector3.Angle(forward, toCandidate);
                    var sqrDistance = toCandidate.sqrMagnitude;

                    // 角度が小さい方を優先し、同角度なら近い方。同一対象の複数コライダはどちらも成り立たず最初の1件が残る
                    // Prefer the smaller angle and the closer one on a tie; extra colliders of one target satisfy neither and the first survives
                    var isBetter = angle < bestAngle || (Mathf.Approximately(angle, bestAngle) && sqrDistance < bestSqrDistance);
                    if (!isBetter) continue;

                    best = candidate;
                    bestAngle = angle;
                    bestSqrDistance = sqrDistance;
                }

                return best;
            }

            bool IsWithinReach(IInteractable interactable)
            {
                return Vector3.Distance(playerPosition, interactable.GameObject.transform.position) <= InteractDistance;
            }

            #endregion
        }
    }
}
