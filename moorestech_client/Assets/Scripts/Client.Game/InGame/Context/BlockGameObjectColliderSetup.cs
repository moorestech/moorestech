using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using UnityEngine;

namespace Client.Game.InGame.Context
{
    /// <summary>
    ///     ブロック生成時のColliderとBlockGameObjectChildのセットアップを行うクラス。
    ///     クリック可能なCollider（Blockレイヤー上で実行時有効、設置判定用を除く）が1つも無いブロックのみ、
    ///     旧来相当のMeshRenderer単位のMeshCollider自動付与にフォールバックする。
    ///     Sets up colliders and BlockGameObjectChild components when a block is created.
    ///     Only when a block has zero clickable colliders (runtime-effective on the Block layer, excluding
    ///     placement-check colliders), fall back to the legacy per-MeshRenderer MeshCollider attachment.
    /// </summary>
    public static class BlockGameObjectColliderSetup
    {
        // フォールバック発動をブロック名ごとに1回だけログする
        // Log fallback activation only once per block name
        private static readonly HashSet<string> LoggedFallbackNames = new();

        public static void SetupColliders(BlockGameObject blockObj)
        {
            // 非アクティブ含む全ColliderにBlockGameObjectChildを付与（後から有効化される子のクリック解決を保証）
            // Attach BlockGameObjectChild to all colliders incl. inactive so later-activated ones resolve clicks
            var blockRoot = blockObj.transform;
            var hasClickableCollider = false;
            foreach (var childCollider in blockObj.GetComponentsInChildren<Collider>(true))
            {
                if (!childCollider.TryGetComponent<BlockGameObjectChild>(out _))
                {
                    childCollider.gameObject.AddComponent<BlockGameObjectChild>();
                }

                if (IsClickableCollider(blockRoot, childCollider)) hasClickableCollider = true;
            }

            // クリック可能Colliderが1つでもあればフォールバック不要
            // No fallback needed if at least one clickable collider exists
            if (hasClickableCollider) return;

            LogFallbackOnce(blockObj);

            // 旧来相当のフォールバック：各MeshRendererへBlockGameObjectChildとMeshColliderを付与
            // Legacy-equivalent fallback: attach BlockGameObjectChild and MeshCollider to each MeshRenderer
            foreach (var meshRenderer in blockObj.GetComponentsInChildren<MeshRenderer>(true))
            {
                // プレビュー専用オブジェクトは設置時に無効化されるため付与しない
                // Skip preview-only objects because they are deactivated on placement
                if (IsUnderPreviewOnly(blockRoot, meshRenderer.transform)) continue;

                if (!meshRenderer.TryGetComponent<BlockGameObjectChild>(out _))
                {
                    meshRenderer.gameObject.AddComponent<BlockGameObjectChild>();
                }
                meshRenderer.gameObject.AddComponent<MeshCollider>();
            }
        }

        /// <summary>
        ///     クリックRaycast（BlockOnlyLayerMask）に当たり得るColliderかを判定する
        ///     Whether the collider can be hit by the click raycast (BlockOnlyLayerMask)
        /// </summary>
        public static bool IsClickableCollider(Transform blockRoot, Collider collider)
        {
            // クリックRaycastはBlockレイヤーのみ対象のため、他レイヤーのColliderはクリック不能
            // The click raycast only targets the Block layer, so colliders on other layers are unclickable
            if (!collider.enabled) return false;
            if (collider.gameObject.layer != LayerConst.BlockLayer) return false;

            // 設置判定専用の小型Colliderはクリック当たりとして数えない
            // Placement-check-only colliders don't count as click targets
            if (collider.TryGetComponent<GroundCollisionDetector>(out _)) return false;

            return IsActiveInBlock(blockRoot, collider.transform) && !IsUnderPreviewOnly(blockRoot, collider.transform);
        }

        private static bool IsActiveInBlock(Transform blockRoot, Transform start)
        {
            // ブロックルートまでの祖先が全てactiveSelfか（ルート自体のSetActiveタイミングに依存しない）
            // Whether all ancestors up to the block root are activeSelf (independent of root SetActive timing)
            for (var t = start; t != null && t != blockRoot; t = t.parent)
            {
                if (!t.gameObject.activeSelf) return false;
            }
            return true;
        }

        private static bool IsUnderPreviewOnly(Transform blockRoot, Transform start)
        {
            // PreviewOnly配下は設置時にSetActive(false)されるため実体として扱わない
            // Preview-only subtrees are deactivated on placement, so they don't count as real
            for (var t = start; t != null && t != blockRoot; t = t.parent)
            {
                if (t.TryGetComponent<IPreviewOnlyObject>(out _)) return true;
            }
            return false;
        }

        private static void LogFallbackOnce(BlockGameObject blockObj)
        {
            if (!LoggedFallbackNames.Add(blockObj.name)) return;
            Debug.Log($"[BlockColliderFallback] クリック可能なColliderが無いためMeshColliderを自動付与します: {blockObj.name}");
        }
    }
}
