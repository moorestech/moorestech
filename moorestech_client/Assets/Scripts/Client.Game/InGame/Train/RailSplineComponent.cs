using Client.Common;
using Client.Game.InGame.Block;
using Server.Util.MessagePack;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Client.Game.InGame.Train
{
    /// <summary>
    /// 単一のレール接続を視覚化するコンポーネント
    /// Component for visualizing a single rail connection
    /// </summary>
    public class RailSplineComponent : MonoBehaviour
    {
        private const string ValidationLogPrefix = "[RailVisualization][Validation]";
        
        private SplineContainer _splineContainer;
        private SplineExtrude _splineExtrude;
        private MeshCollider _meshCollider;
        private RailConnectionDataMessagePack _connectionData;
        private BlockGameObject _startBlock;
        private BlockGameObject _endBlock;
        
        public RailConnectionDataMessagePack ConnectionData => _connectionData;
        public BlockGameObject StartBlock => _startBlock;
        public BlockGameObject EndBlock => _endBlock;
        
        private void Awake()
        {
            // LayerをBlockに設定
            // Set layer to Block
            gameObject.layer = LayerConst.BlockLayer;
            
            // Spline関連コンポーネントを取得または追加
            // Get or add spline-related components
            _splineContainer = GetComponent<SplineContainer>();
            if (_splineContainer == null) _splineContainer = gameObject.AddComponent<SplineContainer>();
            
            _splineExtrude = GetComponent<SplineExtrude>();
            if (_splineExtrude == null)
            {
                _splineExtrude = gameObject.AddComponent<SplineExtrude>();
                _splineExtrude.Container = _splineContainer;
                _splineExtrude.SegmentsPerUnit = 4f;
                _splineExtrude.Sides = 6;
                _splineExtrude.Radius = 0.25f;
                _splineExtrude.Capped = false;
            }
            
            // MeshColliderを追加してConvexを有効化
            // Add MeshCollider and enable Convex
            _meshCollider = GetComponent<MeshCollider>();
            if (_meshCollider == null)
            {
                _meshCollider = gameObject.AddComponent<MeshCollider>();
            }
            _meshCollider.convex = true;
        }
        
        /// <summary>
        /// レール接続データを設定してジオメトリを更新
        /// Set connection data and update spline geometry
        /// </summary>
        public void Initialize(RailConnectionDataMessagePack connectionData, Material material, BlockGameObject startBlock, BlockGameObject endBlock)
        {
            if (connectionData == null)
            {
                Debug.LogWarning($"{ValidationLogPrefix} Cannot initialize RailSplineComponent with null connection data.");
                return;
            }
            
            // データのバリデーションと正規化
            // Validate and normalize connection data
            if (!ValidateAndSanitizeConnection(connectionData))
            {
                Debug.LogWarning($"{ValidationLogPrefix} Invalid connection data provided, skipping initialization.");
                return;
            }
            
            _connectionData = connectionData;
            // レール端点のBlockGameObjectを記録
            // Cache start and end BlockGameObjects for this rail
            _startBlock = startBlock;
            _endBlock = endBlock;
            ApplySplineGeometry();
            ApplyMaterial(material);
            
            // ゲームオブジェクト名を設定
            // Set game object name for debugging
            gameObject.name = $"RailSpline_{FormatComponent(connectionData.FromNode.ComponentId)}_{FormatComponent(connectionData.ToNode.ComponentId)}";
        }
        
        /// <summary>
        /// 接続データを更新してジオメトリを再計算
        /// Update connection data and recalculate geometry
        /// </summary>
        public void UpdateConnection(RailConnectionDataMessagePack connectionData)
        {
            if (connectionData == null)
            {
                Debug.LogWarning($"{ValidationLogPrefix} Cannot update RailSplineComponent with null connection data.");
                return;
            }
            
            if (!ValidateAndSanitizeConnection(connectionData))
            {
                Debug.LogWarning($"{ValidationLogPrefix} Invalid connection data provided, skipping update.");
                return;
            }
            
            _connectionData = connectionData;
            ApplySplineGeometry();
        }
        
        #region Internal
        
        private bool ValidateAndSanitizeConnection(RailConnectionDataMessagePack connection)
        {
            // 接続データのバリデーションと正規化
            // Validate and sanitize connection data
            if (connection.FromNode == null || connection.ToNode == null) return false;
            if (connection.FromNode.ComponentId == null || connection.ToNode.ComponentId == null) return false;
            
            SanitizeControlPoint(connection.FromNode.ControlPoint, "FromNode");
            SanitizeControlPoint(connection.ToNode.ControlPoint, "ToNode");
            
            if (!BezierRailCurveCalculator.ValidateRailConnection(connection.FromNode, connection.ToNode))
            {
                return false;
            }
            
            if (connection.Distance < 0)
            {
                connection.Distance = System.Math.Abs(connection.Distance);
                Debug.LogWarning($"{ValidationLogPrefix} Negative distance detected, using absolute value.");
            }
            
            return true;
        }
        
        private void ApplySplineGeometry()
        {
            // 制御点計算とSplineへの反映
            // Calculate control points and update the spline geometry
            if (_splineContainer == null || _connectionData == null) return;
            
            var spline = _splineContainer.Spline;
            if (spline == null) return;
            
            var controlPoints = BezierRailCurveCalculator.CalculateBezierControlPoints(
                _connectionData.FromNode, 
                _connectionData.ToNode, 
                _connectionData.Distance);
            
            spline.Clear();
            var startKnot = new BezierKnot((float3)controlPoints.p0, float3.zero, (float3)(controlPoints.p1 - controlPoints.p0));
            var endKnot = new BezierKnot((float3)controlPoints.p3, (float3)(controlPoints.p2 - controlPoints.p3), float3.zero);
            spline.Add(startKnot);
            spline.Add(endKnot);
        }
        
        private void ApplyMaterial(Material material)
        {
            // マテリアルを適用
            // Apply material to renderer
            if (material == null) return;
            var renderer = GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = material;
        }
        
        private static void SanitizeControlPoint(RailControlPointMessagePack controlPoint, string label)
        {
            // 制御点の不正値を補正
            // Sanitize control point data to avoid invalid vectors
            if (controlPoint == null) return;
            var original = SanitizeVector((Vector3)controlPoint.OriginalPosition, label, "OriginalPosition");
            var tangent = SanitizeVector((Vector3)controlPoint.ControlPointPosition, label, "ControlPointPosition");
            controlPoint.OriginalPosition = new Vector3MessagePack(original);
            controlPoint.ControlPointPosition = new Vector3MessagePack(tangent);
        }
        
        private static Vector3 SanitizeVector(Vector3 vector, string context, string field)
        {
            // NaNやInfinityを検出してゼロへ補正
            // Clamp NaN or Infinity vectors to zero
            if (float.IsNaN(vector.x) || float.IsNaN(vector.y) || float.IsNaN(vector.z) ||
                float.IsInfinity(vector.x) || float.IsInfinity(vector.y) || float.IsInfinity(vector.z))
            {
                Debug.LogWarning($"{ValidationLogPrefix} Invalid vector detected in {context}.{field}, defaulting to zero.");
                return Vector3.zero;
            }
            return vector;
        }
        
        private static string FormatComponent(RailComponentIDMessagePack componentId)
        {
            // ログ出力用にコンポーネント情報を整形
            // Format component identifier for logging
            var pos = (Vector3Int)componentId.Position;
            return $"{pos.x}_{pos.y}_{pos.z}_{componentId.ID}";
        }
        
        #endregion
    }
}
