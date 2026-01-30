using Client.Common;
using UnityEngine;

/// <summary>
/// 4つのベジェ制御点に沿ってメッシュを変形するコンポーネント。
/// Deforms a mesh along a cubic Bezier curve.
/// </summary>
namespace Client.Game.InGame.Train.RailGraph
{
    [RequireComponent(typeof(MeshFilter))]
    public class BezierRailMesh : MonoBehaviour
    {
        internal const int MaxCurveSamples = 16;
        internal const int CpuMaxCurveSamples = 1024;
        internal const int BoundsSampleCount = 12;
        internal const string MainTexPropertyName = "_MainTex";
        internal const string ColorPropertyName = "_Color";
        internal const string ScanlineSpeedPropertyName = "_ScanlineSpeed";

        [SerializeField] internal Mesh _sourceMesh;
        [SerializeField] internal Vector3 _point0 = new(0f, 0f, 0f);
        [SerializeField] internal Vector3 _point1 = new(0f, 0f, 2f);
        [SerializeField] internal Vector3 _point2 = new(0f, 0f, 4f);
        [SerializeField] internal Vector3 _point3 = new(0f, 0f, 6f);
        [SerializeField] internal Vector3 _forwardAxis = Vector3.forward;
        [SerializeField] internal Vector3 _upAxis = Vector3.up;
        [SerializeField, HideInInspector] internal float _distanceOffset;
        [SerializeField, HideInInspector] internal float _segmentLength = -1f;

        internal MeshFilter _meshFilter;
        internal MeshRenderer _meshRenderer;
        internal MeshCollider _meshCollider;
        internal Mesh _deformedMesh;
        internal Mesh _originalMesh;
        internal Vector3[] _originalVertices;
        internal Vector3[] _originalNormals;
        internal Vector3[] _alignedVertices;
        internal Vector3[] _deformedVertices;
        internal Vector3[] _deformedNormals;
        internal float[] _arcLengths;
        internal float _curveLength;
        internal float[] _arcLengthBuffer;
        internal Quaternion _axisRotation = Quaternion.identity;
        internal float _forwardMin;
        internal float _meshLength;
        internal int _curveSamples = 64;
        internal bool _meshDataDirty = true;
        internal bool _curveDataDirty = true;
        internal bool _useGpuDeform;
        internal MaterialPropertyBlock _propertyBlock;
        internal Material[] _runtimeMaterials;
        internal static Material _previewBaseMaterial;
        internal static bool _previewMaterialLoaded;
        internal Shader _deformShader;
        internal Color _previewColor = MaterialConst.PlaceableColor;

        internal static readonly int DeformP0Id = Shader.PropertyToID("_BezierP0");
        internal static readonly int DeformP1Id = Shader.PropertyToID("_BezierP1");
        internal static readonly int DeformP2Id = Shader.PropertyToID("_BezierP2");
        internal static readonly int DeformP3Id = Shader.PropertyToID("_BezierP3");
        internal static readonly int DeformAxisRotationId = Shader.PropertyToID("_BezierAxisRotation");
        internal static readonly int DeformCurveLengthId = Shader.PropertyToID("_BezierCurveLength");
        internal static readonly int DeformSegmentStartId = Shader.PropertyToID("_BezierSegmentStart");
        internal static readonly int DeformSegmentLengthId = Shader.PropertyToID("_BezierSegmentLength");
        internal static readonly int DeformForwardMinId = Shader.PropertyToID("_BezierForwardMin");
        internal static readonly int DeformMeshLengthId = Shader.PropertyToID("_BezierMeshLength");
        internal static readonly int DeformSampleCountId = Shader.PropertyToID("_BezierSampleCount");
        internal static readonly int DeformArcLengthsId = Shader.PropertyToID("_BezierArcLengths");
        internal static readonly int PreviewColorId = Shader.PropertyToID(MaterialConst.PreviewColorPropertyName);
        internal static readonly int MainTexId = Shader.PropertyToID(MainTexPropertyName);
        internal static readonly int ColorId = Shader.PropertyToID(ColorPropertyName);
        internal static readonly int ScanlineSpeedId = Shader.PropertyToID(ScanlineSpeedPropertyName);

        // 制御点を設定する
        // Set control points
        public void SetControlPoints(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            _point0 = p0;
            _point1 = p1;
            _point2 = p2;
            _point3 = p3;
            _curveDataDirty = true;
        }

        // 参照メッシュを設定する
        // Set source mesh
        public void SetSourceMesh(Mesh mesh)
        {
            if (_sourceMesh == mesh) return;
            _sourceMesh = mesh;
            _originalMesh = null;
            _meshDataDirty = true;
        }

        // 軸方向を設定する
        // Set mesh axes
        public void SetAxes(Vector3 forward, Vector3 up)
        {
            _forwardAxis = forward;
            _upAxis = up;
            _meshDataDirty = true;
        }

        // サンプル数を設定する
        // Set curve sample count
        public void SetSamples(int samples)
        {
            _curveSamples = Mathf.Clamp(samples, 8, CpuMaxCurveSamples);
            _curveDataDirty = true;
        }

        // 曲線データを設定する
        // Set curve data
        public void SetCurveData(float curveLength, float[] arcLengths, int samples)
        {
            _curveLength = curveLength;
            _arcLengths = arcLengths;
            _curveSamples = Mathf.Clamp(samples, 8, CpuMaxCurveSamples);
            _curveDataDirty = false;
        }

        // セグメント範囲を設定する
        // Configure segment range
        public void ConfigureSegment(float distanceOffset, float segmentLength)
        {
            _distanceOffset = Mathf.Max(0f, distanceOffset);
            _segmentLength = segmentLength;
        }

        // GPU変形の有効化を設定する
        // Enable or disable GPU deform
        public void SetUseGpuDeform(bool enable)
        {
            if (_useGpuDeform == enable) return;
            _useGpuDeform = enable;
            _meshDataDirty = true;
            _curveDataDirty = true;
            if (_useGpuDeform) BezierRailMeshData.ReleaseDeformedMesh(this);
            if (!_useGpuDeform) BezierRailMeshMaterials.ReleaseRuntimeMaterials(this);
        }

        // プレビュー色を設定する
        // Set preview color
        public void SetPreviewColor(Color color)
        {
            _previewColor = color;
            if (_useGpuDeform) BezierRailMeshGpu.ApplyPropertyBlock(this);
        }

        // 変形処理を実行する
        // Execute deformation
        public void Deform()
        {
            // 変形モードを切り替える
            // Select deformation mode
            if (_useGpuDeform) { BezierRailMeshGpu.Deform(this); return; }
            BezierRailMeshCpu.Deform(this);
        }

        // モジュール長を推定する
        // Estimate module length
        internal static float CalculateModuleLength(Mesh mesh, Vector3 forwardAxis, Vector3 upAxis) => BezierRailMeshData.CalculateModuleLength(mesh, forwardAxis, upAxis);

        private void Awake()
        {
            TryGetComponent(out _meshFilter);
            TryGetComponent(out _meshRenderer);
            TryGetComponent(out _meshCollider);
        }

        private void OnDestroy()
        {
            // 実行時生成のリソースを破棄する
            // Release runtime resources
            if (!Application.isPlaying && _meshFilter != null && _originalMesh != null) _meshFilter.sharedMesh = _originalMesh;
            BezierRailMeshData.ReleaseDeformedMesh(this);
            BezierRailMeshMaterials.ReleaseRuntimeMaterials(this);
        }
    }
}
