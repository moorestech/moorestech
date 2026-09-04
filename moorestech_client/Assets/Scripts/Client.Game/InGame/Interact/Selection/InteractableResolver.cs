using UnityEngine;

namespace Client.Game.InGame.Interact.Selection
{
    /// <summary>
    ///     コライダから対象を解決。種別は知らずマーカーだけを辿る
    ///     Resolves the interactable behind a hit collider by following markers only, without knowing the kinds
    /// </summary>
    public static class InteractableResolver
    {
        // 距離・角度はピボットではなく当たった面の代表点で測る（原点が足元にある巨木で誤対象を掴むため）
        // Distance and angle are measured at the hit surface, not the pivot, or a tall tree rooted at its feet grabs the wrong target
        internal static bool TryResolve(Collider collider, Vector3 measureFrom, out IInteractable interactable, out Vector3 interactPoint)
        {
            interactPoint = Vector3.zero;

            // コライダか祖先のマーカーが対象を指す
            // The marker on the collider or one of its ancestors points at the target
            var rayTarget = collider.GetComponentInParent<IInteractRayTarget>();
            interactable = rayTarget?.Interactable;

            // 破棄済みGameObjectを指す実体はUnityのfake-null比較で落とす（interface型のnull比較だけでは素通りする）
            // A tombstone pointing at a destroyed GameObject is dropped by Unity's fake-null compare, which an interface-typed null check alone misses
            if (interactable != null && interactable.GameObject != null && interactable.IsInteractAvailable)
            {
                // 非凸MeshColliderでも安全な境界上の最近点を使う
                // The closest point on the bounds is used because it is safe even for a non-convex MeshCollider
                interactPoint = collider.ClosestPointOnBounds(measureFrom);
                return true;
            }

            interactable = null;
            return false;
        }
    }
}
