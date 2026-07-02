using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using UnityEngine;

namespace Client.Game.InGame.Context
{
    /// <summary>
    ///     ブロック生成時のBlockGameObjectChildセットアップと、クリック可能Collider欠落の検出を行うクラス。
    ///     当たり判定はプレハブ側に焼き込む方針（BlockClickColliderBaker）のため、実行時のCollider自動付与は行わない。
    ///     Sets up BlockGameObjectChild on block creation and detects missing clickable colliders.
    ///     Colliders are baked into prefabs (BlockClickColliderBaker), so nothing attaches colliders at runtime.
    /// </summary>
    public static class BlockGameObjectColliderSetup
    {
        // クリック可能Collider欠落をブロック名ごとに1回だけログする
        // Log missing clickable colliders only once per block name
        private static readonly HashSet<string> LoggedMissingColliderNames = new();

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

            // クリック可能Colliderが無いブロックはクリック/削除不能になるため、検出したらエラーログで知らせる
            // A block without any clickable collider can't be clicked/deleted, so report it as an error
            if (hasClickableCollider) return;
            if (!LoggedMissingColliderNames.Add(blockObj.name)) return;
            Debug.LogError($"[BlockCollider] クリック可能なColliderがありません。BlockClickColliderBakerで焼き込んでください: {blockObj.name}");
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

            return IsActiveAndNotPreview(blockRoot, collider.transform);
        }

        /// <summary>
        ///     ブロック内で実行時に有効なオブジェクトか（祖先が全てactiveSelfかつPreviewOnly配下でない）
        ///     Whether the object is runtime-effective within the block (all ancestors activeSelf, not under preview-only)
        /// </summary>
        public static bool IsActiveAndNotPreview(Transform blockRoot, Transform start)
        {
            // PreviewOnly配下は設置時にSetActive(false)されるため実体として扱わない
            // Preview-only subtrees are deactivated on placement, so they don't count as real
            for (var t = start; t != null && t != blockRoot; t = t.parent)
            {
                if (!t.gameObject.activeSelf) return false;
                if (t.TryGetComponent<IPreviewOnlyObject>(out _)) return false;
            }
            return true;
        }
    }
}
