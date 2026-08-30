using Client.Game.InGame.Block;
using Client.Game.InGame.Block.Interact;
using Client.Game.InGame.Entity.Object;
using Client.Game.InGame.Train.View.Object.Core;
using UnityEngine;

namespace Client.Game.InGame.Interact
{
    /// <summary>
    ///     当たったコライダからインタラクト対象を解決する。種別ごとの探し方はここに閉じる
    ///     Resolves the interactable behind a hit collider; per-kind lookup lives only here
    /// </summary>
    public static class InteractableResolver
    {
        public static bool TryResolve(Collider collider, out IInteractable interactable)
        {
            interactable = ResolveByKind();

            // 解決できても選定可能でなければ対象にしない（ハイライトと選定が同じ関門を通る）
            // A resolved but unavailable target is no target, so highlight and selection share one gate
            if (interactable != null && interactable.IsInteractAvailable) return true;

            interactable = null;
            return false;

            #region Internal

            IInteractable ResolveByKind()
            {
                // mapObject・露頭はコライダ上のマーカーで案内される
                // Map objects and outcrops are pointed at by a marker on the collider
                if (collider.TryGetComponent(out IInteractRayTarget rayTarget)) return rayTarget.Interactable;

                // ブロックはメッシュ子から親のインタラクト面へ。開けないブロックには面が無い
                // Blocks climb from a mesh child to the parent's interact face; a non-openable block has none
                var blockChild = collider.GetComponentInParent<BlockGameObjectChild>();
                if (blockChild != null) return blockChild.BlockGameObject.GetComponent<BlockInteractable>();

                // 列車はレンダラー子から車両本体のインタラクト面へ
                // Train cars climb from the renderer child to the car's interact face
                var trainChild = collider.GetComponentInParent<TrainCarEntityChildrenObject>();
                if (trainChild != null) return trainChild.TrainCarEntityObject.GetComponent<TrainCarInteractable>();

                return null;
            }

            #endregion
        }
    }
}
