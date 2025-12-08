using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// GearChainPole接続のプレビューを表示するコンポーネント
    /// Component for displaying GearChainPole connection preview
    /// </summary>
    public class GearChainConnectPreviewObject : MonoBehaviour
    {
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private Material connectableMaterial;
        [SerializeField] private Material notConnectableMaterial;
        [SerializeField] private Material alreadyConnectedMaterial;

        private const int LineSegments = 10;

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }

        /// <summary>
        /// プレビューラインを表示する
        /// Show preview line
        /// </summary>
        public void ShowPreview(GearChainConnectPreviewData data)
        {
            // ラインの色を接続状態に応じて設定する
            // Set line color according to connection state
            UpdateLineMaterial(data.ConnectionState);
            UpdateLinePositions(data.StartPosition, data.EndPosition);

            #region Internal

            void UpdateLineMaterial(GearChainConnectionState state)
            {
                var material = state switch
                {
                    GearChainConnectionState.Connectable => connectableMaterial,
                    GearChainConnectionState.AlreadyConnected => alreadyConnectedMaterial,
                    _ => notConnectableMaterial,
                };
                lineRenderer.material = material;
            }

            void UpdateLinePositions(Vector3 start, Vector3 end)
            {
                lineRenderer.positionCount = LineSegments;
                for (var i = 0; i < LineSegments; i++)
                {
                    var rate = (float)i / (LineSegments - 1);
                    var point = Vector3.Lerp(start, end, rate);
                    lineRenderer.SetPosition(i, point);
                }
            }

            #endregion
        }
    }

    /// <summary>
    /// GearChainPole接続プレビューのデータ
    /// Data for GearChainPole connection preview
    /// </summary>
    public struct GearChainConnectPreviewData
    {
        public Vector3 StartPosition { get; }
        public Vector3 EndPosition { get; }
        public GearChainConnectionState ConnectionState { get; }
        public int RequiredChainCount { get; }
        public bool HasEnoughChainItems { get; }

        public GearChainConnectPreviewData(
            Vector3 startPosition,
            Vector3 endPosition,
            GearChainConnectionState connectionState,
            int requiredChainCount,
            bool hasEnoughChainItems)
        {
            StartPosition = startPosition;
            EndPosition = endPosition;
            ConnectionState = connectionState;
            RequiredChainCount = requiredChainCount;
            HasEnoughChainItems = hasEnoughChainItems;
        }
    }

    /// <summary>
    /// GearChainPole接続状態
    /// GearChainPole connection state
    /// </summary>
    public enum GearChainConnectionState
    {
        /// <summary>
        /// 接続可能
        /// Connectable
        /// </summary>
        Connectable,

        /// <summary>
        /// 接続不可（距離超過、接続数上限など）
        /// Not connectable (distance exceeded, connection limit, etc.)
        /// </summary>
        NotConnectable,

        /// <summary>
        /// 既に接続済み
        /// Already connected
        /// </summary>
        AlreadyConnected,
    }
}
