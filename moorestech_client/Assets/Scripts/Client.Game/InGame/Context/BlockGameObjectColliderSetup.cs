using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using UnityEngine;

namespace Client.Game.InGame.Context
{
    /// <summary>
    ///     ブロック生成時のColliderとBlockGameObjectChildのセットアップを行うクラス
    ///     Sets up colliders and BlockGameObjectChild components when a block is created
    /// </summary>
    public static class BlockGameObjectColliderSetup
    {
        /// <summary>
        ///     既存Colliderを持つ子にBlockGameObjectChildを付与する
        ///     実行時有効なColliderが1つも無いブロックのみ、旧来のMeshCollider自動付与にフォールバックする
        ///     Attach BlockGameObjectChild to children that already have a Collider.
        ///     Only when a block has zero runtime-effective colliders, fall back to the legacy runtime MeshCollider attachment.
        /// </summary>
        public static void SetupColliders(BlockGameObject blockObj)
        {
            // 既にColliderがある子にのみBlockGameObjectChildを付ける
            // Attach BlockGameObjectChild only to children that already have a Collider
            var hasRuntimeEffectiveCollider = false;
            foreach (var childCollider in blockObj.GetComponentsInChildren<Collider>())
            {
                // 同一オブジェクトに複数Colliderがあっても二重付与しない
                // Avoid double-attaching when one object has multiple Colliders
                if (!childCollider.TryGetComponent<BlockGameObjectChild>(out _))
                {
                    childCollider.gameObject.AddComponent<BlockGameObjectChild>();
                }

                if (IsRuntimeEffectiveCollider(childCollider)) hasRuntimeEffectiveCollider = true;
            }

            // 実行時有効なColliderがあればフォールバック不要
            // No fallback needed if any runtime-effective collider exists
            if (hasRuntimeEffectiveCollider) return;

            // 旧来相当のフォールバック：各MeshRendererへBlockGameObjectChildとMeshColliderを付与
            // Legacy-equivalent fallback: attach BlockGameObjectChild and MeshCollider to each MeshRenderer
            foreach (var meshRenderer in blockObj.GetComponentsInChildren<MeshRenderer>())
            {
                // プレビュー専用オブジェクトは設置時に無効化されるため付与しない
                // Skip preview-only objects because they are deactivated on placement
                if (meshRenderer.GetComponentInParent<IPreviewOnlyObject>() != null) continue;

                if (!meshRenderer.TryGetComponent<BlockGameObjectChild>(out _))
                {
                    meshRenderer.gameObject.AddComponent<BlockGameObjectChild>();
                }
                if (!meshRenderer.TryGetComponent<Collider>(out _))
                {
                    meshRenderer.gameObject.AddComponent<MeshCollider>();
                }
            }

            #region Internal

            bool IsRuntimeEffectiveCollider(Collider collider)
            {
                // PreviewOnly配下は設置時にSetActive(false)されるため実体Colliderとして数えない
                // Colliders under preview-only objects are deactivated on placement, so don't count them as real
                return collider.enabled && collider.GetComponentInParent<IPreviewOnlyObject>() == null;
            }

            #endregion
        }
    }
}
