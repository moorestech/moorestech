using UnityEngine;

/// <summary>
/// 4制御点のベジエ曲線に沿ってメッシュを変形するコンポーネント。
/// 区間を指定できるので、複数モジュールを並べたレールにも利用できる。
/// </summary>
namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail
{
    [RequireComponent(typeof(MeshFilter))]
    public class BezierRailMesh : MonoBehaviour
    {
        [SerializeField] private Mesh _sourceMesh;
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

        /// <summary>制御点を一括設定して再変形する。</summary>
        public void SetControlPoints(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            _point0 = p0;
            _point1 = p1;
            _point2 = p2;
            _point3 = p3;
        }

        /// <summary>使用する元メッシュを差し替える。</summary>
        public void SetSourceMesh(Mesh mesh)
        {
            if (_sourceMesh == mesh)
                return;

            _sourceMesh = mesh;
            _originalMesh = null;
            _deformedMesh = null;
        }

        /// <summary>軸設定を変更する。</summary>
        public void SetAxes(Vector3 forward, Vector3 up)
        {
            _forwardAxis = forward;
            _upAxis = up;
        }

        /// <summary>ベジエのサンプル数を設定する。</summary>
        public void SetSamples(int samples)
        {
            _curveSamples = Mathf.Clamp(samples, 8, 1024);
        }

        /// <summary>曲線上のどの距離区間を使用するか設定する。</summary>
        public void ConfigureSegment(float distanceOffset, float segmentLength)
        {
            _distanceOffset = Mathf.Max(0f, distanceOffset);
            _segmentLength = segmentLength;
        }

        public void Deform()
        {
            if (!EnsureMeshData())
                return;

            if (!BuildArcLengthTable())
                return;

            var axisRotation = BuildAxisRotation();
            if (_alignedVertices == null || _alignedVertices.Length != _originalVertices.Length)
                _alignedVertices = new Vector3[_originalVertices.Length];

            float forwardMin = float.PositiveInfinity;
            float forwardMax = float.NegativeInfinity;

            // 元メッシュの前方向範囲を取得
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
            TryGetComponent(out _meshCollider);
        }

        private void Start()
        {
            Deform();
        }

        private void OnDestroy()
        {
            if (!Application.isPlaying && _meshFilter != null && _originalMesh != null)
                _meshFilter.sharedMesh = _originalMesh;
            if (_deformedMesh == null)
                return;
            Destroy(_deformedMesh);
            _deformedMesh = null;
        }

        private bool EnsureMeshData()
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
            var f = tangent.sqrMagnitude > 1e-6f ? tangent.normalized : Vector3.forward;
            var up = _upAxis.sqrMagnitude > 1e-4f ? _upAxis.normalized : Vector3.up;

            if (Mathf.Abs(Vector3.Dot(f, up)) > 0.999f)
            {
                up = Vector3.Cross(f, Vector3.right);
                if (up.sqrMagnitude < 1e-6f)
                    up = Vector3.Cross(f, Vector3.up);
                up.Normalize();
            }

            return Quaternion.LookRotation(f, up);
        }

        private bool BuildArcLengthTable()
        {
            var steps = Mathf.Max(8, _curveSamples);
            _arcLengths ??= new float[steps + 1];
            if (_arcLengths.Length != steps + 1)
                _arcLengths = new float[steps + 1];

            _arcLengths[0] = 0f;
            var previous = EvaluatePosition(0f);
            var total = 0f;

            for (var i = 1; i <= steps; i++)
            {
                var t = (float)i / steps;
                var current = EvaluatePosition(t);
                total += Vector3.Distance(previous, current);
                _arcLengths[i] = total;
                previous = current;
            }

            _curveLength = total;
            return _curveLength > 1e-4f;
        }

        private float DistanceToTime(float distance)
        {
            if (_curveLength <= 1e-5f)
                return 0f;

            distance = Mathf.Clamp(distance, 0f, _curveLength);
            var steps = _arcLengths.Length - 1;

            for (var i = 1; i <= steps; i++)
            {
                var prev = _arcLengths[i - 1];
                var current = _arcLengths[i];
                if (distance > current)
                    continue;

                var lerp = Mathf.Approximately(current, prev) ? 0f : (distance - prev) / (current - prev);
                var stepSize = 1f / steps;
                return Mathf.Lerp((i - 1) * stepSize, i * stepSize, lerp);
            }

            return 1f;
        }

        private Vector3 EvaluatePosition(float t) => EvaluatePosition(_point0, _point1, _point2, _point3, t);

        private Vector3 EvaluateTangent(float t) => EvaluateTangent(_point0, _point1, _point2, _point3, t);

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

        internal static Vector3 EvaluatePosition(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            t = Mathf.Clamp01(t);
            var u = 1f - t;
            return (u * u * u) * p0
                 + (3f * u * u * t) * p1
                 + (3f * u * t * t) * p2
                 + (t * t * t) * p3;
        }

        internal static Vector3 EvaluateTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            t = Mathf.Clamp01(t);
            var u = 1f - t;

            var term0 = (p1 - p0) * (3f * u * u);
            var term1 = (p2 - p1) * (6f * u * t);
            var term2 = (p3 - p2) * (3f * t * t);
            var derivative = term0 + term1 + term2;

            return derivative.sqrMagnitude > 1e-6f ? derivative.normalized : Vector3.forward;
        }

        internal static float EstimateCurveLength(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int samples)
        {
            samples = Mathf.Max(8, samples);
            var total = 0f;
            var prev = EvaluatePosition(p0, p1, p2, p3, 0f);
            for (var i = 1; i <= samples; i++)
            {
                var t = (float)i / samples;
                var current = EvaluatePosition(p0, p1, p2, p3, t);
                total += Vector3.Distance(prev, current);
                prev = current;
            }

            return total;
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