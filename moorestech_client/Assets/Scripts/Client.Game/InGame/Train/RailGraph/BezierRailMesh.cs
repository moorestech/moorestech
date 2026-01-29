using Client.Common;
using Game.Train.RailCalc;
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
        private const int CpuMaxCurveSamples = 1024;
        private const string BaseMapPropertyName = "_BaseMap";
        private const string BaseColorPropertyName = "_BaseColor";
        private const string ScanlineSpeedPropertyName = "_ScanlineSpeed";
        private const string AlphaPropertyName = "_Alpha";

        [SerializeField] private Mesh _sourceMesh;
        [SerializeField] private Vector3 _point0 = new(0f, 0f, 0f);
        [SerializeField] private Vector3 _point1 = new(0f, 0f, 2f);
        [SerializeField] private Vector3 _point2 = new(0f, 0f, 4f);
        [SerializeField] private Vector3 _point3 = new(0f, 0f, 6f);
        [SerializeField] private Vector3 _forwardAxis = Vector3.forward;
        [SerializeField] private Vector3 _upAxis = Vector3.up;
        [SerializeField, HideInInspector] private float _distanceOffset;
        [SerializeField, HideInInspector] private float _segmentLength = -1f;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MeshCollider _meshCollider;
        private Mesh _deformedMesh;
        private Mesh _originalMesh;
        private Vector3[] _originalVertices;
        private Vector3[] _originalNormals;
        private Vector3[] _alignedVertices;
        private Vector3[] _deformedVertices;
        private Vector3[] _deformedNormals;
        private float[] _arcLengths;
        private float _curveLength;
        private float[] _arcLengthBuffer;
        private Quaternion _axisRotation = Quaternion.identity;
        private float _forwardMin;
        private float _meshLength;
        private int _curveSamples = 64;
        private bool _meshDataDirty = true;
        private bool _curveDataDirty = true;
        private bool _useGpuDeform;
        private MaterialPropertyBlock _propertyBlock;
        private Material[] _runtimeMaterials;
        private Shader _deformShader;
        private Color _previewColor = MaterialConst.PlaceableColor;

        private static readonly int DeformP0Id = Shader.PropertyToID("_BezierP0");
        private static readonly int DeformP1Id = Shader.PropertyToID("_BezierP1");
        private static readonly int DeformP2Id = Shader.PropertyToID("_BezierP2");
        private static readonly int DeformP3Id = Shader.PropertyToID("_BezierP3");
        private static readonly int DeformAxisRotationId = Shader.PropertyToID("_BezierAxisRotation");
        private static readonly int DeformCurveLengthId = Shader.PropertyToID("_BezierCurveLength");
        private static readonly int DeformSegmentStartId = Shader.PropertyToID("_BezierSegmentStart");
        private static readonly int DeformSegmentLengthId = Shader.PropertyToID("_BezierSegmentLength");
        private static readonly int DeformForwardMinId = Shader.PropertyToID("_BezierForwardMin");
        private static readonly int DeformMeshLengthId = Shader.PropertyToID("_BezierMeshLength");
        private static readonly int DeformSampleCountId = Shader.PropertyToID("_BezierSampleCount");
        private static readonly int DeformArcLengthsId = Shader.PropertyToID("_BezierArcLengths");
        private static readonly int PreviewColorId = Shader.PropertyToID(MaterialConst.PreviewColorPropertyName);
        private static readonly int BaseMapId = Shader.PropertyToID(BaseMapPropertyName);
        private static readonly int BaseColorId = Shader.PropertyToID(BaseColorPropertyName);
        private static readonly int ScanlineSpeedId = Shader.PropertyToID(ScanlineSpeedPropertyName);
        private static readonly int AlphaId = Shader.PropertyToID(AlphaPropertyName);

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
            if (_useGpuDeform) ReleaseDeformedMesh();
            if (!_useGpuDeform) ReleaseRuntimeMaterials();
        }

        // プレビュー色を設定する
        // Set preview color
        public void SetPreviewColor(Color color)
        {
            _previewColor = color;
            if (_useGpuDeform) ApplyPropertyBlock();
        }

        // 変形処理を実行する
        // Execute deformation
        public void Deform()
        {
            // 変形モードを切り替える
            // Select deformation mode
            if (_useGpuDeform) { DeformGpu(); return; }
            DeformCpu();
        }

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
            ReleaseDeformedMesh();
            ReleaseRuntimeMaterials();
        }

        #region Internal

        // GPU変形を実行する
        // Deform with GPU
        private void DeformGpu()
        {
            // 必須データを準備する
            // Ensure required data
            if (!EnsureMeshData()) return;
            if (!EnsureCurveData()) return;

            // シェーダーパラメータを適用する
            // Apply shader parameters
            ApplyPropertyBlock();
        }

        // CPU変形を実行する
        // Deform with CPU
        private void DeformCpu()
        {
            // 必須データを準備する
            // Ensure required data
            if (!EnsureMeshData()) return;
            if (!EnsureCurveData()) return;
            EnsureWorkingBuffers();

            // 変形対象の範囲を決定する
            // Resolve segment range
            var targetSegmentLength = _segmentLength > 0f ? Mathf.Min(_segmentLength, _curveLength) : _curveLength;
            var startDistance = Mathf.Clamp(_distanceOffset, 0f, _curveLength);
            var endDistance = Mathf.Clamp(startDistance + targetSegmentLength, 0f, _curveLength);
            var usableLength = Mathf.Max(1e-4f, endDistance - startDistance);

            if (_alignedVertices == null || _alignedVertices.Length != _originalVertices.Length) _alignedVertices = new Vector3[_originalVertices.Length];

            // 頂点ごとにベジェ変形を行う
            // Deform each vertex along Bezier
            for (var i = 0; i < _alignedVertices.Length; i++)
            {
                var alignedVertex = _axisRotation * _originalVertices[i];
                _alignedVertices[i] = alignedVertex;
                var normalizedForward = (alignedVertex.z - _forwardMin) / _meshLength;
                var distanceOnCurve = startDistance + normalizedForward * usableLength;

                var t = DistanceToTime(distanceOnCurve);
                var curvePos = EvaluatePosition(t);
                var curveTangent = EvaluateTangent(t);
                var rotation = BuildCurveRotation(curveTangent);

                var offset = rotation * new Vector3(alignedVertex.x, alignedVertex.y, 0f);
                _deformedVertices[i] = curvePos + offset;

                var normal = _axisRotation * _originalNormals[i];
                _deformedNormals[i] = rotation * normal;
            }

            // メッシュへ反映する
            // Apply to mesh
            _deformedMesh.vertices = _deformedVertices;
            _deformedMesh.normals = _deformedNormals;
            _deformedMesh.RecalculateBounds();

            if (_meshCollider != null) _meshCollider.sharedMesh = _deformedMesh;
        }

        // メッシュ関連情報を準備する
        // Prepare mesh data
        private bool EnsureMeshData()
        {
            if (_meshFilter == null && !TryGetComponent(out _meshFilter)) return false;
            if (_meshRenderer == null) TryGetComponent(out _meshRenderer);
            if (_meshCollider == null) TryGetComponent(out _meshCollider);
            if (_sourceMesh == null) _sourceMesh = _meshFilter.sharedMesh;
            if (_sourceMesh == null) return false;

            if (_originalMesh != _sourceMesh)
            {
                _originalMesh = _sourceMesh;
                _originalVertices = _originalMesh.vertices;
                _originalNormals = _originalMesh.normals;
                _deformedVertices = null;
                _deformedNormals = null;
                _meshDataDirty = true;
            }

            if (_useGpuDeform)
            {
                if (_meshFilter.sharedMesh != _sourceMesh) _meshFilter.sharedMesh = _sourceMesh;
            }
            else
            {
                if (_deformedMesh == null)
                {
                    _deformedMesh = Instantiate(_sourceMesh);
                    _deformedMesh.name = $"{_sourceMesh.name}_Bezier";
                }
                if (_meshFilter.sharedMesh != _deformedMesh) _meshFilter.sharedMesh = _deformedMesh;
            }

            if (_meshDataDirty) RefreshMeshMetrics();

            if (_meshLength <= 1e-4f) return false;
            if (_useGpuDeform) return EnsureMaterial();
            return _originalVertices != null && _originalNormals != null;
        }

        // 曲線データを準備する
        // Prepare curve data
        private bool EnsureCurveData()
        {
            if (!_curveDataDirty && _arcLengths != null && _curveLength > 1e-4f) return true;

            var samples = GetCurveSamples();
            _curveLength = BezierUtility.BuildArcLengthTable(_point0, _point1, _point2, _point3, samples, ref _arcLengths);
            _curveSamples = samples;
            _curveDataDirty = false;
            return _curveLength > 1e-4f;
        }

        // 頂点バッファを準備する
        // Ensure working buffers
        private void EnsureWorkingBuffers()
        {
            if (_deformedVertices == null || _deformedVertices.Length != _originalVertices.Length) _deformedVertices = new Vector3[_originalVertices.Length];
            if (_deformedNormals == null || _deformedNormals.Length != _originalNormals.Length) _deformedNormals = new Vector3[_originalNormals.Length];
        }

        // メッシュ軸情報を更新する
        // Refresh mesh axis metrics
        private void RefreshMeshMetrics()
        {
            _axisRotation = GetAxisRotation(_forwardAxis, _upAxis);
            if (_sourceMesh == null) { _meshLength = 0f; _meshDataDirty = false; return; }

            var vertices = _sourceMesh.vertices;
            if (vertices == null || vertices.Length == 0) { _meshLength = 0f; _meshDataDirty = false; return; }

            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;

            // 前方軸の範囲を測定する
            // Measure range along forward axis
            for (var i = 0; i < vertices.Length; i++)
            {
                var aligned = _axisRotation * vertices[i];
                var z = aligned.z;
                if (z < min) min = z;
                if (z > max) max = z;
            }

            _forwardMin = min;
            _meshLength = Mathf.Max(1e-4f, max - min);
            _meshDataDirty = false;
        }

        // GPU用シェーダーを準備する
        // Prepare deformation shader
        private bool EnsureMaterial()
        {
            var shader = ResolveDeformShader();
            if (shader == null) return false;
            if (_meshRenderer == null && !TryGetComponent(out _meshRenderer)) return false;

            var baseMaterials = _meshRenderer.sharedMaterials;
            if (baseMaterials == null || baseMaterials.Length == 0) baseMaterials = new[] { _meshRenderer.sharedMaterial };

            var isRuntimeAssigned = ReferenceEquals(_meshRenderer.sharedMaterials, _runtimeMaterials);
            if (_runtimeMaterials == null || _runtimeMaterials.Length != baseMaterials.Length || !isRuntimeAssigned) RebuildRuntimeMaterials(baseMaterials, shader);
            else ApplyShaderToRuntimeMaterials(shader);

            if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
            return _runtimeMaterials != null && _runtimeMaterials.Length > 0;
        }

        // 変形シェーダーを解決する
        // Resolve deformation shader
        private Shader ResolveDeformShader()
        {
            if (_deformShader != null) return _deformShader;
            _deformShader = Shader.Find("RailPreview/BezierDeform");
            if (_deformShader == null) Debug.LogError("[BezierRailMesh] Deformation shader not found.");
            return _deformShader;
        }

        // ランタイムマテリアルを再構築する
        // Rebuild runtime materials
        private void RebuildRuntimeMaterials(Material[] baseMaterials, Shader shader)
        {
            ReleaseRuntimeMaterials();
            if (baseMaterials == null || baseMaterials.Length == 0) return;

            _runtimeMaterials = new Material[baseMaterials.Length];

            // 元マテリアルをコピーして置換する
            // Copy base materials and replace shader
            for (var i = 0; i < baseMaterials.Length; i++)
            {
                var baseMaterial = baseMaterials[i];
                var runtime = new Material(shader);
                if (baseMaterial != null) runtime.CopyPropertiesFromMaterial(baseMaterial);
                ApplyPreviewDefaults(runtime);
                _runtimeMaterials[i] = runtime;
            }

            _meshRenderer.sharedMaterials = _runtimeMaterials;
        }

        // 既存マテリアルのシェーダーを更新する
        // Update shader on existing materials
        private void ApplyShaderToRuntimeMaterials(Shader shader)
        {
            if (_runtimeMaterials == null) return;

            // シェーダーのみ差し替える
            // Replace shader only
            for (var i = 0; i < _runtimeMaterials.Length; i++)
            {
                var runtime = _runtimeMaterials[i];
                if (runtime == null) continue;
                if (runtime.shader == shader) continue;
                runtime.shader = shader;
                ApplyPreviewDefaults(runtime);
            }
        }

        // プレビュー用の初期値を適用する
        // Apply preview defaults
        private void ApplyPreviewDefaults(Material runtime)
        {
            if (runtime == null) return;
            if (runtime.HasProperty(BaseMapId)) runtime.SetTexture(BaseMapId, null);
            if (runtime.HasProperty(BaseColorId)) runtime.SetColor(BaseColorId, Color.white);
            if (runtime.HasProperty(ScanlineSpeedId)) runtime.SetFloat(ScanlineSpeedId, 10f);
            if (runtime.HasProperty(AlphaId)) runtime.SetFloat(AlphaId, 1f);
            if (runtime.HasProperty(PreviewColorId)) runtime.SetColor(PreviewColorId, _previewColor);
        }

        // ランタイムマテリアルを解放する
        // Release runtime materials
        private void ReleaseRuntimeMaterials()
        {
            if (_runtimeMaterials == null) return;

            for (var i = 0; i < _runtimeMaterials.Length; i++)
            {
                if (_runtimeMaterials[i] == null) continue;
                Destroy(_runtimeMaterials[i]);
                _runtimeMaterials[i] = null;
            }

            _runtimeMaterials = null;
        }

        // CPU変形メッシュを解放する
        // Release CPU deformed mesh
        private void ReleaseDeformedMesh()
        {
            if (_deformedMesh == null) return;
            Destroy(_deformedMesh);
            _deformedMesh = null;
            if (_meshFilter != null && _sourceMesh != null) _meshFilter.sharedMesh = _sourceMesh;
        }

        // シェーダーパラメータを適用する
        // Apply shader parameters
        private void ApplyPropertyBlock()
        {
            if (_meshRenderer == null && !TryGetComponent(out _meshRenderer)) return;
            if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
            _propertyBlock.Clear();

            var targetSegmentLength = _segmentLength > 0f ? Mathf.Min(_segmentLength, _curveLength) : _curveLength;
            var startDistance = Mathf.Clamp(_distanceOffset, 0f, _curveLength);
            var endDistance = Mathf.Clamp(startDistance + targetSegmentLength, 0f, _curveLength);
            var usableLength = Mathf.Max(1e-4f, endDistance - startDistance);

            UpdateArcLengthBuffer();

            // 制御点をローカル空間に変換する
            // Convert control points into local space
            var localP0 = transform.InverseTransformPoint(_point0);
            var localP1 = transform.InverseTransformPoint(_point1);
            var localP2 = transform.InverseTransformPoint(_point2);
            var localP3 = transform.InverseTransformPoint(_point3);

            _propertyBlock.SetVector(DeformP0Id, localP0);
            _propertyBlock.SetVector(DeformP1Id, localP1);
            _propertyBlock.SetVector(DeformP2Id, localP2);
            _propertyBlock.SetVector(DeformP3Id, localP3);
            _propertyBlock.SetVector(DeformAxisRotationId, new Vector4(_axisRotation.x, _axisRotation.y, _axisRotation.z, _axisRotation.w));
            _propertyBlock.SetFloat(DeformCurveLengthId, _curveLength);
            _propertyBlock.SetFloat(DeformSegmentStartId, startDistance);
            _propertyBlock.SetFloat(DeformSegmentLengthId, usableLength);
            _propertyBlock.SetFloat(DeformForwardMinId, _forwardMin);
            _propertyBlock.SetFloat(DeformMeshLengthId, _meshLength);
            _propertyBlock.SetFloat(DeformSampleCountId, _curveSamples);
            _propertyBlock.SetFloatArray(DeformArcLengthsId, _arcLengthBuffer);
            _propertyBlock.SetColor(PreviewColorId, _previewColor);

            _meshRenderer.SetPropertyBlock(_propertyBlock);
        }

        // アーク長テーブルを固定長配列に転写する
        // Copy arc-length table into fixed buffer
        private void UpdateArcLengthBuffer()
        {
            if (_arcLengthBuffer == null || _arcLengthBuffer.Length != MaxCurveSamples + 1) _arcLengthBuffer = new float[MaxCurveSamples + 1];

            var source = _arcLengths;
            var copyLength = source == null ? 0 : Mathf.Min(source.Length, _arcLengthBuffer.Length);

            // 不足分は0で埋める
            // Fill missing slots with zeros
            for (var i = 0; i < copyLength; i++) _arcLengthBuffer[i] = source[i];
            for (var i = copyLength; i < _arcLengthBuffer.Length; i++) _arcLengthBuffer[i] = 0f;
        }

        // サンプル数を取得する
        // Get effective curve samples
        private int GetCurveSamples()
        {
            var maxSamples = _useGpuDeform ? MaxCurveSamples : CpuMaxCurveSamples;
            return Mathf.Clamp(_curveSamples, 8, maxSamples);
        }

        private Quaternion BuildCurveRotation(Vector3 tangent)
        {
            var forward = tangent.sqrMagnitude > 1e-6f ? tangent.normalized : Vector3.forward;
            var horizontal = new Vector3(forward.x, 0f, forward.z);

            // ロールを抑制してレールの姿勢を安定させる
            // Keep rails upright without roll
            if (horizontal.sqrMagnitude < 1e-6f)
            {
                var angle = forward.y >= 0f ? 90f : -90f;
                return Quaternion.AngleAxis(angle, Vector3.right);
            }

            var yawRotation = Quaternion.LookRotation(horizontal.normalized, Vector3.up);
            var invYaw = Quaternion.Inverse(yawRotation);
            var localForward = invYaw * forward;
            var pitchAngle = Mathf.Atan2(localForward.y, Mathf.Max(1e-6f, localForward.z)) * Mathf.Rad2Deg;
            return yawRotation * Quaternion.AngleAxis(pitchAngle, Vector3.right);
        }

        private float DistanceToTime(float distance) => BezierUtility.DistanceToTime(distance, _curveLength, _arcLengths);

        private Vector3 EvaluatePosition(float t) => BezierUtility.GetBezierPoint(_point0, _point1, _point2, _point3, t);

        private Vector3 EvaluateTangent(float t) => BezierUtility.GetBezierTangent(_point0, _point1, _point2, _point3, t);

        internal static Quaternion GetAxisRotation(Vector3 forwardAxis, Vector3 upAxis)
        {
            var forward = forwardAxis.sqrMagnitude > 1e-4f ? forwardAxis.normalized : Vector3.forward;
            var up = upAxis.sqrMagnitude > 1e-4f ? upAxis.normalized : Vector3.up;

            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.999f)
            {
                up = Vector3.Cross(forward, Vector3.right);
                if (up.sqrMagnitude < 1e-4f) up = Vector3.Cross(forward, Vector3.up);
                up = up.normalized;
            }

            return Quaternion.Inverse(Quaternion.LookRotation(forward, up));
        }

        internal static float CalculateModuleLength(Mesh mesh, Vector3 forwardAxis, Vector3 upAxis)
        {
            if (mesh == null) return 0f;

            var vertices = mesh.vertices;
            if (vertices == null || vertices.Length == 0) return 0f;

            var axisRotation = GetAxisRotation(forwardAxis, upAxis);
            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;

            foreach (var vertex in vertices)
            {
                var aligned = axisRotation * vertex;
                if (aligned.z < min) min = aligned.z;
                if (aligned.z > max) max = aligned.z;
            }

            return Mathf.Max(1e-4f, max - min);
        }

        #endregion
    }
}
