using UnityEngine;

namespace Client.Game.InGame.Interact
{
    /// <summary>
    ///     コライダから対象を解決。種別は知らずマーカーだけを辿る
    ///     Resolves the interactable behind a hit collider by following markers only, without knowing the kinds
    /// </summary>
    public static class InteractableResolver
    {
        internal static bool TryResolve(Collider collider, out IInteractable interactable)
        {
            // コライダ自身か祖先のマーカーが対象を案内する
            // The marker on the collider or one of its ancestors points at the target
            var rayTarget = collider.GetComponentInParent<IInteractRayTarget>();
            interactable = rayTarget?.Interactable;

            // 解決できても選定可能でなければ対象にしない（ハイライトと選定が同じ関門を通る）
            // A resolved but unavailable target is no target, so highlight and selection share one gate
            if (interactable != null && interactable.IsInteractAvailable) return true;

            interactable = null;
            return false;
        }
    }
}
