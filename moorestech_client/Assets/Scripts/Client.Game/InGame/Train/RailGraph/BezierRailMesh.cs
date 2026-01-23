using Game.Train.RailCalc;
using UnityEngine;

/// <summary>
/// 4制御点のベジエ曲線に沿ってメチE��ュを変形するコンポ�Eネント、E
/// 区間を持E��できるので、褁E��モジュールを並べたレールにも利用できる、E
/// </summary>
namespace Client.Game.InGame.Train.RailGraph
{
    [RequireComponent(typeof(MeshFilter))]
    public class BezierRailMesh : MonoBehaviour
    {
        internal const int MaxCurveSamples = 16;

        [SerializeField] private Mesh _sourceMesh;
        [SerializeField] private Shader _deformShader;
        [SerializeField] private Vector3 _point0 = new(0f, 0f, 0f);
        [SerializeField] private Vector3 _point1 = new(0f, 0f, 2f);
        [SerializeField] private Vector3 _point2 = new(0f, 0f, 4f);
        [SerializeField] private Vector3 _point3 = new(0f, 0f, 6f);
        [SerializeField] private Vector3 _forwardAxis = Vector3.forward;
        [SerializeField] private Vector3 _upAxis = Vector3.up;
        private int _curveSamples = 64;
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
        private Quaternion _axisRotation = Quaternion.identity;
        private float _forwardMin;
        private float _meshLength;
        private float _crossSectionRadius;
        private float[] _arcLengths;
        private float[] _arcLengthBuffer;
        private float _curveLength;
        private bool _meshDataDirty = true;
        private bool _curveDataDirty = true;
        private bool _useGpuDeform;
        private bool _usePreviewColor;
        private Color _previewColor = Color.white;
        private MaterialPropertyBlock _propertyBlock;
        private Material[] _runtimeMaterials;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
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

        /// <summary>制御点を一括設定して再変形する、E/summary>
        public void SetControlPoints(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            _point0 = p0;
            _point1 = p1;
            _point2 = p2;
            _point3 = p3;
            _curveDataDirty = true;
        }

        /// <summary>使用する允E��チE��ュを差し替える、E/summary>
        public void SetSourceMesh(Mesh mesh)
        {
            if (_sourceMesh == mesh)
                return;

            _sourceMesh = mesh;
            _originalMesh = null;
            _deformedMesh = null;
            _meshDataDirty = true;
        }

        /// <summary>軸設定を変更する、E/summary>
        public void SetAxes(Vector3 forward, Vector3 up)
        {
            _forwardAxis = forward;
            _upAxis = up;
            _meshDataDirty = true;
        }

        /// <summary>ベジエのサンプル数を設定する、E/summary>
        public void SetSamples(int samples)
        {
            _curveSamples = ClampCurveSamples(samples);
            _curveDataDirty = true;
        }

        /// <summary>曲線上�Eどの距離区間を使用するか設定する、E/summary>
        public void ConfigureSegment(float distanceOffset, float segmentLength)
        {
            _distanceOffset = Mathf.Max(0f, distanceOffset);
            _segmentLength = segmentLength;
        }

        /// <summary>距離とアークレンスを辛㿡したチE��チE��設定する、E/summary>
        public void SetCurveData(float curveLength, float[] arcLengths, int samples)
        {
            _curveLength = curveLength;
            _arcLengths = arcLengths;
            _curveSamples = ClampCurveSamples(samples);
            _curveDataDirty = false;
        }

        /// <summary>GPU変形の期間を�定する、E/summary>
        public void SetUseGpuDeform(bool useGpuDeform)
        {
            _useGpuDeform = useGpuDeform;
            _meshDataDirty = true;
            _curveDataDirty = true;
        }

        /// <summary>プレビュー用の色を設定する/Set preview color.</summary>
        public void SetPreviewColor(Color color)
        {
            _previewColor = color;
            _usePreviewColor = true;
        }

        public void Deform()
        {
            // GPU変形の場合�EシェーダにはプロパティブロチE��のみを行います、E
            // For GPU deformation, push parameters through material property blocks.
            if (_useGpuDeform)
            {
                if (!EnsureMeshDataGpu())
                    return;
                if (!EnsureCurveData())
                    return;
                // GPU変形のシェーダーパラメータを反映する
                // Apply shader parameters for GPU deformation
                ApplyPropertyBlock();
                // GPU変形のカリング用Boundsを更新する
                // Update renderer bounds for GPU deformation culling
                UpdateGpuBounds();
                return;
            }

            if (!EnsureMeshDataCpu())
                return;

            if (!EnsureCurveData())
                return;

            var axisRotation = BuildAxisRotation();
            if (_alignedVertices == null || _alignedVertices.Length != _originalVertices.Length)
                _alignedVertices = new Vector3[_originalVertices.Length];

            float forwardMin = float.PositiveInfinity;
            float forwardMax = float.NegativeInfinity;

            // 允E��チE��ュの前方向篁E��を取征E
            // Project vertices onto the forward axis to measure span
            for (var i = 0; i < _originalVertices.Length; i++)
            {
                var aligned = axisRotation * _originalVertices[i];
                _alignedVertices[i] = aligned;
                var z = aligned.z;
                if (z < forwardMin) forwardMin = z;
                if (z > forwardMax) forwardMax = z;
            }

            var meshLength = Mathf.Max(1e-4f, forwardMax - forwardMin);
            if (_curveLength <= 1e-4f)
                return;

            var targetSegmentLength = _segmentLength > 0f ? Mathf.Min(_segmentLength, _curveLength) : _curveLength;
            var startDistance = Mathf.Clamp(_distanceOffset, 0f, _curveLength);
            var endDistance = Mathf.Clamp(startDistance + targetSegmentLength, 0f, _curveLength);
            var usableLength = Mathf.Max(1e-4f, endDistance - startDistance);

            EnsureWorkingBuffers();

            for (var i = 0; i < _alignedVertices.Length; i++)
            {
                var alignedVertex = _alignedVertices[i];
                var normalizedForward = (alignedVertex.z - forwardMin) / meshLength;
                var distanceOnCurve = startDistance + normalizedForward * usableLength;

                var t = DistanceToTime(distanceOnCurve);
                var curvePos = EvaluatePosition(t);
                var curveTangent = EvaluateTangent(t);
                var rotation = BuildCurveRotation(curveTangent);

                var offset = rotation * new Vector3(alignedVertex.x, alignedVertex.y, 0f);
                _deformedVertices[i] = curvePos + offset;

                var normal = axisRotation * _originalNormals[i];
                _deformedNormals[i] = rotation * normal;
            }

            _deformedMesh.vertices = _deformedVertices;
            _deformedMesh.normals = _deformedNormals;
            _deformedMesh.RecalculateBounds();

            if (_meshCollider != null)
                _meshCollider.sharedMesh = _deformedMesh;
        }

        private void Awake()
        {
            TryGetComponent(out _meshFilter);
            TryGetComponent(out _meshRenderer);
            TryGetComponent(out _meshCollider);
        }

        private void OnDestroy()
        {
            ReleaseRuntimeMaterials();
            if (!Application.isPlaying && _meshFilter != null && _originalMesh != null)
                _meshFilter.sharedMesh = _originalMesh;
            if (_deformedMesh == null)
                return;
            Destroy(_deformedMesh);
            _deformedMesh = null;
        }

        private bool EnsureMeshDataCpu()
        {
            if (_meshFilter == null && !TryGetComponent(out _meshFilter))
                return false;

            if (_meshCollider == null)
                TryGetComponent(out _meshCollider);

            if (_sourceMesh == null)
                _sourceMesh = _meshFilter.sharedMesh;

            if (_sourceMesh == null)
                return false;

            if (_originalMesh != _sourceMesh)
            {
                _originalMesh = _sourceMesh;
                _originalVertices = _originalMesh.vertices;
                _originalNormals = _originalMesh.normals;
                _deformedVertices = null;
                _deformedNormals = null;
            }

            if (_deformedMesh == null)
            {
                _deformedMesh = Instantiate(_sourceMesh);
                _deformedMesh.name = $"{_sourceMesh.name}_Bezier";
            }

            _meshFilter.mesh = _deformedMesh;
            return _originalVertices != null && _originalNormals != null;
        }

        private bool EnsureMeshDataGpu()
        {
            if (_meshFilter == null && !TryGetComponent(out _meshFilter))
                return false;
            if (_meshRenderer == null && !TryGetComponent(out _meshRenderer))
                return false;
            if (_meshCollider == null)
                TryGetComponent(out _meshCollider);

            if (_sourceMesh == null)
                _sourceMesh = _meshFilter.sharedMesh;

            if (_sourceMesh == null)
                return false;

            if (_originalMesh != _sourceMesh)
            {
                _originalMesh = _sourceMesh;
                _meshDataDirty = true;
            }

            if (_meshFilter.sharedMesh != _sourceMesh)
                _meshFilter.sharedMesh = _sourceMesh;

            if (_meshDataDirty)
                RefreshMeshMetrics();

            return _meshLength > 1e-4f && EnsureMaterial();
        }

        private void EnsureWorkingBuffers()
        {
            if (_deformedVertices == null || _deformedVertices.Length != _originalVertices.Length)
                _deformedVertices = new Vector3[_originalVertices.Length];

            if (_deformedNormals == null || _deformedNormals.Length != _originalNormals.Length)
                _deformedNormals = new Vector3[_originalNormals.Length];
        }

        private Quaternion BuildAxisRotation() => GetAxisRotation(_forwardAxis, _upAxis);

        private Quaternion BuildCurveRotation(Vector3 tangent)
        {
            var forward = tangent.sqrMagnitude > 1e-6f ? tangent.normalized : Vector3.forward;
            var horizontal = new Vector3(forward.x, 0f, forward.z);

            // レール姿勢をヨー→ピチE��の頁E��構築してローリングを抑制
            // Build yaw first then pitch to keep rails upright without roll
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

        private bool EnsureCurveData()
        {
            if (!_curveDataDirty && _arcLengths != null && _curveLength > 1e-4f)
                return true;

            _curveSamples = ClampCurveSamples(_curveSamples);
            _curveLength = BezierUtility.BuildArcLengthTable(_point0, _point1, _point2, _point3, _curveSamples, ref _arcLengths);
            _curveDataDirty = false;
            return _curveLength > 1e-4f;
        }

        private float DistanceToTime(float distance) => BezierUtility.DistanceToTime(distance, _curveLength, _arcLengths);

        private Vector3 EvaluatePosition(float t) => BezierUtility.GetBezierPoint(_point0, _point1, _point2, _point3, t);

        private Vector3 EvaluateTangent(float t) => BezierUtility.GetBezierTangent(_point0, _point1, _point2, _point3, t);

        private int ClampCurveSamples(int samples)
        {
            var maxSamples = _useGpuDeform ? MaxCurveSamples : 1024;
            return Mathf.Clamp(samples, 8, maxSamples);
        }

        private bool EnsureMaterial()
        {
            var shader = ResolveDeformShader();
            if (shader == null)
                return false;

            var baseMaterials = _meshRenderer.sharedMaterials;
            if (baseMaterials == null || baseMaterials.Length == 0)
                baseMaterials = new[] { _meshRenderer.sharedMaterial };

            var isRuntimeAssigned = ReferenceEquals(_meshRenderer.sharedMaterials, _runtimeMaterials);
            if (_runtimeMaterials == null || _runtimeMaterials.Length != baseMaterials.Length || !isRuntimeAssigned)
                RebuildRuntimeMaterials(baseMaterials, shader);
            else
                ApplyShaderToRuntimeMaterials(shader);

            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();

            return _runtimeMaterials != null && _runtimeMaterials.Length > 0;
        }

        private Shader ResolveDeformShader()
        {
            if (_deformShader != null)
                return _deformShader;

            _deformShader = Shader.Find("RailPreview/BezierDeform");
            if (_deformShader == null)
                Debug.LogError("[BezierRailMesh] Deformation shader not found.");
            return _deformShader;
        }

        private void RebuildRuntimeMaterials(Material[] baseMaterials, Shader shader)
        {
            ReleaseRuntimeMaterials();
            if (baseMaterials == null || baseMaterials.Length == 0)
                return;

            _runtimeMaterials = new Material[baseMaterials.Length];
            for (var i = 0; i < baseMaterials.Length; i++)
            {
                var baseMaterial = baseMaterials[i];
                var runtime = new Material(shader);
                if (baseMaterial != null)
                    runtime.CopyPropertiesFromMaterial(baseMaterial);
                _runtimeMaterials[i] = runtime;
            }

            _meshRenderer.sharedMaterials = _runtimeMaterials;
        }

        private void ApplyShaderToRuntimeMaterials(Shader shader)
        {
            if (_runtimeMaterials == null)
                return;

            for (var i = 0; i < _runtimeMaterials.Length; i++)
            {
                var runtime = _runtimeMaterials[i];
                if (runtime == null)
                    continue;
                if (runtime.shader == shader)
                    continue;
                runtime.shader = shader;
            }
        }

        private void ReleaseRuntimeMaterials()
        {
            if (_runtimeMaterials == null)
                return;

            for (var i = 0; i < _runtimeMaterials.Length; i++)
            {
                if (_runtimeMaterials[i] == null)
                    continue;
                Destroy(_runtimeMaterials[i]);
                _runtimeMaterials[i] = null;
            }

            _runtimeMaterials = null;
        }

        private void RefreshMeshMetrics()
        {
            _axisRotation = GetAxisRotation(_forwardAxis, _upAxis);
            if (_sourceMesh == null)
            {
                _meshLength = 0f;
                return;
            }

            var vertices = _sourceMesh.vertices;
            if (vertices == null || vertices.Length == 0)
            {
                _meshLength = 0f;
                return;
            }

            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;
            var radiusSqr = 0f;

            // メチE��ュの前方�͈�を取征E
            // Measure range along the forward axis
            for (var i = 0; i < vertices.Length; i++)
            {
                var aligned = _axisRotation * vertices[i];
                var z = aligned.z;
                if (z < min) min = z;
                if (z > max) max = z;
                var radialSqr = aligned.x * aligned.x + aligned.y * aligned.y;
                if (radialSqr > radiusSqr) radiusSqr = radialSqr;
            }

            _forwardMin = min;
            _meshLength = Mathf.Max(1e-4f, max - min);
            _crossSectionRadius = Mathf.Sqrt(radiusSqr);
            _meshDataDirty = false;
        }

        private void ApplyPropertyBlock()
        {
            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();
            _propertyBlock.Clear();

            var targetSegmentLength = _segmentLength > 0f ? Mathf.Min(_segmentLength, _curveLength) : _curveLength;
            var startDistance = Mathf.Clamp(_distanceOffset, 0f, _curveLength);
            var endDistance = Mathf.Clamp(startDistance + targetSegmentLength, 0f, _curveLength);
            var usableLength = Mathf.Max(1e-4f, endDistance - startDistance);

            UpdateArcLengthBuffer();

            // ����_����[�J����Ԃɕϊ�����
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

            // プレビュー色がある場合はベース色を上書きする
            // Override base color when preview color is enabled
            if (_usePreviewColor)
                _propertyBlock.SetColor(BaseColorId, _previewColor);

            _meshRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void UpdateGpuBounds()
        {
            // GPU変形のローカルBoundsを近似的に更新する
            // Update local bounds approximation for GPU deformation
            if (_meshRenderer == null)
                return;
            if (_curveLength <= 1e-4f)
                return;

            var targetSegmentLength = _segmentLength > 0f ? Mathf.Min(_segmentLength, _curveLength) : _curveLength;
            var startDistance = Mathf.Clamp(_distanceOffset, 0f, _curveLength);
            var endDistance = Mathf.Clamp(startDistance + targetSegmentLength, 0f, _curveLength);
            var usableLength = Mathf.Max(1e-4f, endDistance - startDistance);
            var sampleCount = Mathf.Clamp(_curveSamples, 4, MaxCurveSamples);
            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            // 曲線上をサンプルしてBoundsを更新する
            // Sample the curve and update bounds
            for (var i = 0; i <= sampleCount; i++)
            {
                var distance = startDistance + usableLength * i / sampleCount;
                var t = DistanceToTime(distance);
                var worldPos = EvaluatePosition(t);
                var localPos = transform.InverseTransformPoint(worldPos);
                var expand = Vector3.one * _crossSectionRadius;
                min = Vector3.Min(min, localPos - expand);
                max = Vector3.Max(max, localPos + expand);
            }

            if (float.IsPositiveInfinity(min.x))
                return;

            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            _meshRenderer.localBounds = bounds;
        }

        private void UpdateArcLengthBuffer()
        {
            if (_arcLengthBuffer == null || _arcLengthBuffer.Length != MaxCurveSamples + 1)
                _arcLengthBuffer = new float[MaxCurveSamples + 1];

            var source = _arcLengths;
            var copyLength = source == null ? 0 : Mathf.Min(source.Length, _arcLengthBuffer.Length);
            for (var i = 0; i < copyLength; i++)
                _arcLengthBuffer[i] = source[i];
            for (var i = copyLength; i < _arcLengthBuffer.Length; i++)
                _arcLengthBuffer[i] = 0f;
        }

        internal static Quaternion GetAxisRotation(Vector3 forwardAxis, Vector3 upAxis)
        {
            var forward = forwardAxis.sqrMagnitude > 1e-4f ? forwardAxis.normalized : Vector3.forward;
            var up = upAxis.sqrMagnitude > 1e-4f ? upAxis.normalized : Vector3.up;

            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.999f)
            {
                up = Vector3.Cross(forward, Vector3.right);
                if (up.sqrMagnitude < 1e-4f)
                    up = Vector3.Cross(forward, Vector3.up);
                up = up.normalized;
            }

            return Quaternion.Inverse(Quaternion.LookRotation(forward, up));
        }

        internal static float CalculateModuleLength(Mesh mesh, Vector3 forwardAxis, Vector3 upAxis)
        {
            if (mesh == null)
                return 0f;

            var vertices = mesh.vertices;
            if (vertices == null || vertices.Length == 0)
                return 0f;

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
    }

}
