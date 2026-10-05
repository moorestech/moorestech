using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Client.Game.InGame.Context;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.GearPole
{
    /// <summary>
    /// 単一のチェーン接続を表示するコンポーネント
    /// Component for displaying a single chain connection
    /// </summary>
    public class GearChainPoleChainLineViewElement : MonoBehaviour, IConnectionLineViewElement
    {
        private const float LineSpacing = 0.1f;
        private const float ColliderRadius = 0.08f;

        // 未解決時の再解決を試みる間隔
        // Interval between resolution retries while unresolved
        private const float RetryIntervalSeconds = 0.5f;

        [SerializeField] private LineRenderer lineRenderer1;
        [SerializeField] private LineRenderer lineRenderer2;

        private BlockInstanceId _startInstanceId;
        private BlockInstanceId _endInstanceId;
        private float _retryTimer;

        /// <summary>
        /// 接続ラインの位置を設定する
        /// Set the positions of the connection lines
        /// </summary>
        public void SetLine(BlockInstanceId startInstanceId, BlockInstanceId endInstanceId)
        {
            _startInstanceId = startInstanceId;
            _endInstanceId = endInstanceId;

            // 即座に解決できなければUpdateでの遅延再試行に委ねる
            // If not resolvable immediately, defer to the retry loop in Update
            enabled = !TryBuildLine();
        }

        private void Update()
        {
            // 未解決の間のみ一定間隔で相手ブロックの生成を再確認する
            // While unresolved, periodically recheck whether the partner block has been created
            _retryTimer -= Time.deltaTime;
            if (0f < _retryTimer) return;
            _retryTimer = RetryIntervalSeconds;

            if (TryBuildLine()) enabled = false;
        }

        // 両端ブロックの解決と線・コライダー構築を試みる。相手が未生成ならfalseを返す
        // Attempt to resolve both endpoints and build lines and collider; returns false if the partner is not yet created
        private bool TryBuildLine()
        {
            // BlockGameObjectDataStoreから座標を取得
            // Get positions from BlockGameObjectDataStore
            if (!ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(_startInstanceId, out var startBlock) ||
                !ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(_endInstanceId, out var endBlock))
            {
                return false;
            }

            // ブロックの中心座標を計算
            // Calculate block center positions
            var startPos = startBlock.transform.position + new Vector3(0.5f, 0.5f, 0.5f);
            var endPos = endBlock.transform.position + new Vector3(0.5f, 0.5f, 0.5f);

            // 2本のラインを水平方向にオフセット
            // Offset two lines horizontally
            var direction = (endPos - startPos).normalized;
            var right = Vector3.Cross(Vector3.up, direction).normalized;
            if (right == Vector3.zero) right = Vector3.right;

            var offset = right * (LineSpacing / 2f);

            // ライン1（右側）
            // Line 1 (right side)
            lineRenderer1.positionCount = 2;
            lineRenderer1.SetPosition(0, startPos + offset);
            lineRenderer1.SetPosition(1, endPos + offset);

            // ライン2（左側）
            // Line 2 (left side)
            lineRenderer2.positionCount = 2;
            lineRenderer2.SetPosition(0, startPos - offset);
            lineRenderer2.SetPosition(1, endPos - offset);

            // 削除ツールが狙えるよう、両端を結ぶトリガーカプセルを接続線レイヤーに置く
            // Place a trigger capsule spanning both ends on the connection-line layer so the delete tool can aim at it
            ConnectionLineColliderBuilder.AddCapsule(transform, (startPos + endPos) * 0.5f, endPos - startPos, ColliderRadius, Vector3.Distance(startPos, endPos));
            return true;
        }
    }
}
