using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Context;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire
{
    /// <summary>
    /// 単一の電力ワイヤー接続をカテナリー曲線で表示するコンポーネント
    /// Component that renders a single electric wire connection as a catenary curve
    /// </summary>
    public class ElectricWireLineViewElement : MonoBehaviour
    {
        // ワイヤーの垂れ量は両端距離に比例させる
        // Wire sag is proportional to the distance between endpoints
        private const float SagRatio = 0.1f;
        private static readonly Vector3 BlockCenterOffset = new(0.5f, 0.5f, 0.5f);

        [SerializeField] private MeshFilter meshFilter;

        private Mesh _generatedMesh;

        // 両端のブロックインスタンスID（Task 11の切断が参照する契約）
        // Both endpoint block instance IDs (contract referenced by Task 11 cutting)
        public BlockInstanceId FromId { get; private set; }
        public BlockInstanceId ToId { get; private set; }

        /// <summary>
        /// 接続ワイヤーの両端を設定し、曲線メッシュとコライダーを構築する
        /// Set the wire endpoints and build the curve mesh and colliders
        /// </summary>
        public void SetLine(BlockInstanceId fromId, BlockInstanceId toId)
        {
            FromId = fromId;
            ToId = toId;

            // 両端ブロックの座標をBlockGameObjectDataStoreから解決する
            // Resolve endpoint positions from the BlockGameObjectDataStore
            if (!ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(fromId, out var fromBlock)) return;
            if (!ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(toId, out var toBlock)) return;

            var start = fromBlock.transform.position + BlockCenterOffset;
            var end = toBlock.transform.position + BlockCenterOffset;

            // メッシュはワールド座標で構築するため、自身の姿勢を原点に揃える
            // Reset own pose to origin since the mesh is built in world space
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;

            // カテナリーメッシュとクリック判定用セグメントを生成する
            // Generate the catenary mesh and click-detection segments
            var sag = Vector3.Distance(start, end) * SagRatio;
            var colliderSegments = new List<(Vector3 center, Vector3 up, float length)>();
            _generatedMesh = CatenaryWireMeshBuilder.Build(start, end, sag, colliderSegments);

            meshFilter.mesh = _generatedMesh;
            BuildColliders(colliderSegments);
        }

        private void OnDestroy()
        {
            // 動的生成したメッシュを破棄してリークを防ぐ
            // Destroy the dynamically generated mesh to prevent a leak
            if (_generatedMesh != null) Destroy(_generatedMesh);
        }

        // セグメント情報に沿ってクリック判定用のCapsuleColliderを配置する
        // Place CapsuleColliders for click detection along the segment info
        private void BuildColliders(List<(Vector3 center, Vector3 up, float length)> colliderSegments)
        {
            foreach (var segment in colliderSegments)
            {
                // 専用レイヤに置き、既存のブロック操作レイキャストへの干渉を防ぐ
                // Place on the dedicated layer to avoid interfering with existing block-operation raycasts
                var colliderObject = new GameObject("WireCollider");
                colliderObject.layer = LayerConst.ElectricWireLayer;

                var colliderTransform = colliderObject.transform;
                colliderTransform.SetParent(transform, false);

                // カプセルのローカルY軸をセグメント軸方向へ向ける
                // Orient the capsule's local Y axis along the segment axis
                colliderTransform.position = segment.center;
                colliderTransform.rotation = Quaternion.FromToRotation(Vector3.up, segment.up);

                // トリガー化してプレイヤーとの物理衝突を防ぐ（レイキャストにはヒットする）
                // Make it a trigger to avoid physical collision with the player (still hit by raycasts)
                var capsule = colliderObject.AddComponent<CapsuleCollider>();
                capsule.isTrigger = true;
                capsule.direction = 1;
                capsule.radius = CatenaryWireMeshBuilder.WireRadius;
                capsule.height = segment.length;
            }
        }
    }
}
