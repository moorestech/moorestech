using System;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.Control;
using Client.Game.InGame.Control.ViewMode;
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

        // レイはカメラ始点で三人称ではプレイヤーの背後から伸びるため、到達距離はインタラクト距離より十分長く取る
        // The ray starts at the camera, which sits behind the player in third person, so it must reach well beyond the interact distance
        private const float RayLength = 100f;
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
                var ray = camera.ScreenPointToRay(AimPointProvider.GetAimScreenPoint());
                var hits = Physics.RaycastAll(ray, RayLength, InteractLayerMask);
                if (hits.Length == 0) return null;
                Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

                foreach (var hit in hits)
                {
                    // 手前の設置ゴーストだけ貫通する（BlockClickDetectUtilと同じ規則）
                    // Only the placement ghost in front is see-through (same rule as BlockClickDetectUtil)
                    if (hit.collider.GetComponentInParent<BlockPreviewObject>() != null) continue;

                    // 最前面の実体が届く対象でなければ照準ヒット無しとして近傍探索へ回す
                    // If the frontmost solid is not a reachable target, fall through to the nearby search
                    if (!InteractableResolver.TryResolve(hit.collider, out var interactable)) return null;
                    return IsWithinReach(interactable) ? interactable : null;
                }

                return null;
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
