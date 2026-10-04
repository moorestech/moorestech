using Client.Common;
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
        private const int CapsuleDirectionYAxis = 1;
        private const float ColliderRadius = 0.08f;

        [SerializeField] private LineRenderer lineRenderer1;
        [SerializeField] private LineRenderer lineRenderer2;

        /// <summary>
        /// 接続ラインの位置を設定する
        /// Set the positions of the connection lines
        /// </summary>
        public void SetLine(BlockInstanceId startInstanceId, BlockInstanceId endInstanceId)
        {
            // BlockGameObjectDataStoreから座標を取得
            // Get positions from BlockGameObjectDataStore
            if (!ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(startInstanceId, out var startBlock) ||
                !ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(endInstanceId, out var endBlock))
            {
                Debug.LogWarning($"[GearChainLine] endpoint block not found: from={startInstanceId} to={endInstanceId}");
                return;
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
            BuildCollider(startPos, endPos);

            #region Internal

            void BuildCollider(Vector3 start, Vector3 end)
            {
                var colliderObject = new GameObject("ChainCollider");
                colliderObject.layer = LayerConst.ConnectionLineLayer;
                colliderObject.transform.SetParent(transform, false);
                colliderObject.transform.position = (start + end) * 0.5f;
                colliderObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, end - start);

                var capsule = colliderObject.AddComponent<CapsuleCollider>();
                capsule.isTrigger = true;
                capsule.direction = CapsuleDirectionYAxis;
                capsule.radius = ColliderRadius;
                capsule.height = Vector3.Distance(start, end);
            }

            #endregion
        }
    }
}
