using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.GearPole
{
    /// <summary>
    /// 単一のチェーン接続を表示するコンポーネント
    /// Component for displaying a single chain connection
    /// </summary>
    public class GearChainPoleChainLineViewElement : MonoBehaviour
    {
        private const float LineSpacing = 0.1f;

        [SerializeField] private LineRenderer lineRenderer1;
        [SerializeField] private LineRenderer lineRenderer2;

        /// <summary>
        /// 接続ラインの位置を設定する
        /// Set the positions of the connection lines
        /// </summary>
        public void SetPositions(Vector3 startPos, Vector3 endPos)
        {
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
        }
    }
}
