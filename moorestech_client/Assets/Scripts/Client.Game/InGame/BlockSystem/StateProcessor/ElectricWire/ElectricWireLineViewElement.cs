using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Client.Game.InGame.Context;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire
{
    /// <summary>
    /// 単一の電力ワイヤー接続をカテナリー曲線で表示するコンポーネント
    /// Component that renders a single electric wire connection as a catenary curve
    /// </summary>
    public class ElectricWireLineViewElement : MonoBehaviour, IConnectionLineViewElement
    {
        // ワイヤーの垂れ量は両端距離に比例させる
        // Wire sag is proportional to the distance between endpoints
        private const float SagRatio = 0.1f;
        // 未解決時の再解決を試みる間隔
        // Interval between resolution retries while unresolved
        private const float RetryIntervalSeconds = 0.5f;
        // CapsuleColliderのdirectionはローカルY軸を表す1
        // CapsuleCollider direction value 1 means the local Y axis
        private const int CapsuleDirectionYAxis = 1;
        [SerializeField] private MeshFilter meshFilter;

        private Mesh _generatedMesh;
        private bool _isResolved;
        private float _retryTimer;

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

            // 即座に解決できなければUpdateでの遅延再試行に委ねる
            // If not resolvable immediately, defer to the retry loop in Update
            _isResolved = TryBuildLine();
            enabled = !_isResolved;
        }

        private void Update()
        {
            // 未解決の間のみ一定間隔でパートナーブロックの生成を再確認する
            // While unresolved, periodically recheck whether the partner block has been created
            _retryTimer -= Time.deltaTime;
            if (0f < _retryTimer) return;
            _retryTimer = RetryIntervalSeconds;

            if (!TryBuildLine()) return;
            _isResolved = true;
            enabled = false;
        }

        // 両端ブロックの解決とメッシュ構築を試みる。相手が未生成ならfalseを返す
        // Attempt to resolve both endpoints and build the mesh; returns false if the partner is not yet created
        private bool TryBuildLine()
        {
            // 両端ブロックの座標を解決する
            // Resolve endpoint positions from the BlockGameObjectDataStore
            if (!ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(FromId, out var fromBlock)) return false;
            if (!ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(ToId, out var toBlock)) return false;

            var start = ResolveEndpoint(fromBlock);
            var end = ResolveEndpoint(toBlock);

            // メッシュはワールド座標で構築するため、自身の姿勢を原点に揃える
            // Reset own pose to origin since the mesh is built in world space
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;

            // メッシュとクリック判定セグメントを生成
            // Generate the catenary mesh and click-detection segments
            var sag = Vector3.Distance(start, end) * SagRatio;
            var colliderSegments = new List<(Vector3 center, Vector3 up, float length)>();
            _generatedMesh = CatenaryWireMeshBuilder.Build(start, end, sag, colliderSegments);

            meshFilter.mesh = _generatedMesh;
            BuildColliders();
            return true;

            #region Internal

            // 専用接続点があればそこへ、無ければブロック上面中央へ接続する
            // Connect to the dedicated point when present, otherwise to the block top center
            Vector3 ResolveEndpoint(BlockGameObject block)
            {
                var connectionPoint = block.GetComponentInChildren<ElectricWireConnectionPoint>(true);
                if (connectionPoint != null) return connectionPoint.transform.position;

                var min = block.BlockPosInfo.MinPos;
                var max = block.BlockPosInfo.MaxPos + Vector3Int.one;
                return new Vector3((min.x + max.x) * 0.5f, max.y, (min.z + max.z) * 0.5f);
            }

            // セグメント情報に沿ってクリック判定用のCapsuleColliderを配置する
            // Place CapsuleColliders for click detection along the segment info
            void BuildColliders()
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
                    capsule.direction = CapsuleDirectionYAxis;
                    capsule.radius = CatenaryWireMeshBuilder.WireRadius;
                    capsule.height = segment.length;
                }
            }

            #endregion
        }

        private void OnDestroy()
        {
            // 動的生成したメッシュを破棄してリークを防ぐ
            // Destroy the dynamically generated mesh to prevent a leak
            if (_generatedMesh != null) Destroy(_generatedMesh);
        }
    }
}
