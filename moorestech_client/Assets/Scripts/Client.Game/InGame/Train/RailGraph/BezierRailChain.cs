using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Block;
using Game.Train.RailCalc;
using UnityEngine;

/// <summary>
/// ベジェ曲線に沿ったレールメッシュをセグメントで生成する
/// Hosts multiple BezierRailMesh segments under a single spline definition
/// </summary>
namespace Client.Game.InGame.Train.RailGraph
{
    public class BezierRailChain : MonoBehaviour
    {
        [SerializeField] private GameObject _modulePrefab;
        [SerializeField] private GameObject _halfModulePrefab;
        [SerializeField] private GameObject _quarterModulePrefab;
        [SerializeField] private GameObject _eighthModulePrefab;

        private Vector3 _point0 = new(0f, 0f, 0f);
        private Vector3 _point1 = new(0f, 0f, 2f);
        private Vector3 _point2 = new(0f, 0f, 4f);
        private Vector3 _point3 = new(0f, 0f, 6f);
        [SerializeField] private Vector3 _forwardAxis = Vector3.forward;
        [SerializeField] private Vector3 _upAxis = Vector3.up;
        private int _curveSamples = 64;

        private readonly List<SegmentInstance> _segments = new();

        private RailGraphClientCache _railGraphClientCache;
        private RendererMaterialReplacerController _controller;
        private Material _removeMaterial;
        private float _curveLength;
        private float[] _arcLengths;
        private bool _useMeshCollider = true;
        private bool _useGpuDeform;
        private bool _enableDeleteTarget = true;
        private bool _enableRemovePreviewMaterial = true;
        private bool _usePreviewColor;
        private Color _previewColor = Color.white;

        private void Awake()
        {
            // 削除プレビュー用のマテリアルをロードする
            // Load remove preview material
            _removeMaterial = Resources.Load<Material>(MaterialConst.PreviewPlaceBlockMaterial);
        }

        public void SetRailGraphCache(RailGraphClientCache cache)
        {
            _railGraphClientCache = cache;
        }

        // コライダー生成の有無を設定する
        // Configure mesh collider usage
        public void SetUseMeshCollider(bool useMeshCollider)
        {
            _useMeshCollider = useMeshCollider;
        }

        // GPU変形モードを設定する
        // Configure GPU deformation mode
        public void SetUseGpuDeform(bool useGpuDeform)
        {
            _useGpuDeform = useGpuDeform;
            ApplyDeformModeToMeshes();
        }

        // 削除対象コンポーネントの付与を設定する
        // Configure delete target components
        public void SetEnableDeleteTarget(bool enableDeleteTarget)
        {
            _enableDeleteTarget = enableDeleteTarget;
        }

        // 削除プレビュー用マテリアルの生成を設定する
        // Configure remove-preview material controller
        public void SetEnableRemovePreviewMaterial(bool enableRemovePreviewMaterial)
        {
            _enableRemovePreviewMaterial = enableRemovePreviewMaterial;
        }

        // プレビュー色を設定する
        // Set preview color
        public void SetPreviewColor(Color color)
        {
            _previewColor = color;
            _usePreviewColor = true;
            ApplyPreviewColorToMeshes();
        }

        // 曲線サンプル数を設定する
        // Set curve sampling resolution
        public void SetCurveSamples(int samples)
        {
            _curveSamples = Mathf.Clamp(samples, 8, 1024);
        }

        /// <summary>
        /// 制御点を設定する
        /// Set the control points
        /// </summary>
        public void SetControlPoints(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            _point0 = p0;
            _point1 = p1;
            _point2 = p2;
            _point3 = p3;
        }

        public void Rebuild()
        {
            // 既存セグメントを破棄して再構築する
            // Clear existing segments and rebuild
            ClearSegmentsImmediate();
            if (_modulePrefab == null)
                return;

            // モジュール長を取得して有効性を確認する
            // Resolve module length and validate
            var moduleLength = GetModuleLength(_modulePrefab, 0f);
            if (moduleLength <= 0f)
                return;

            // 曲線テーブルを構築して長さを求める
            // Build arc-length table and curve length
            var curveSamples = ResolveCurveSamples();
            _curveLength = BezierUtility.BuildArcLengthTable(_point0, _point1, _point2, _point3, curveSamples, ref _arcLengths);
            if (_curveLength <= 1e-4f)
                return;

            var offset = 0f;
            var fullSegmentCount = Mathf.Max(0, Mathf.FloorToInt(_curveLength / moduleLength));

            // 完全長のセグメントを配置する
            // Place full-length segments
            for (var i = 0; i < fullSegmentCount; i++)
            {
                var segment = CreateSegmentGO(i, _modulePrefab);
                ConfigureSegmentInstance(segment, offset, moduleLength, curveSamples);
                offset += moduleLength;
            }

            // 端数分のセグメントを配置する
            // Place remaining partial segments
            var remainder = Mathf.Max(0f, _curveLength - offset);
            if (remainder <= 1e-4f)
                return;

            var remainderSteps = Mathf.Clamp(Mathf.RoundToInt(remainder / moduleLength * 8f), 1, 7);
            var halfLength = _halfModulePrefab != null ? GetModuleLength(_halfModulePrefab, moduleLength * 0.5f) : moduleLength * 0.5f;
            var quarterLength = _quarterModulePrefab != null ? GetModuleLength(_quarterModulePrefab, moduleLength * 0.25f) : moduleLength * 0.25f;
            var eighthLength = _eighthModulePrefab != null ? GetModuleLength(_eighthModulePrefab, moduleLength * 0.125f) : moduleLength * 0.125f;
            TryCreatePartialSegment(ref remainderSteps, 4, _halfModulePrefab, halfLength, ref offset, curveSamples);
            TryCreatePartialSegment(ref remainderSteps, 2, _quarterModulePrefab, quarterLength, ref offset, curveSamples);
            TryCreatePartialSegment(ref remainderSteps, 1, _eighthModulePrefab, eighthLength, ref offset, curveSamples);
            if (remainderSteps > 0)
                Debug.LogWarning($"[BezierRailChain] Remaining steps were not filled: {remainderSteps}", this);

            // プレビュー用のマテリアル制御を初期化する
            // Initialize preview material controller
            if (_enableRemovePreviewMaterial)
                _controller = new RendererMaterialReplacerController(gameObject);
        }

        // GPU変形時にシェーダーパラメータを再適用する
        // Reapply GPU deformation parameters
        public void RefreshGpuDeform()
        {
            if (!_useGpuDeform)
                return;

            for (var i = 0; i < _segments.Count; i++)
            {
                var segment = _segments[i];
                for (var j = 0; j < segment.Meshes.Count; j++)
                    segment.Meshes[j].Deform();
            }
        }

        private void OnDestroy()
        {
            ClearSegmentsImmediate();
        }

        private void OnDisable()
        {
            ClearSegmentsImmediate();
        }

        private void ClearSegmentsImmediate()
        {
            // 生成済みセグメントを全て破棄する
            // Dispose every spawned segment instance
            for (var i = _segments.Count - 1; i >= 0; i--)
                DestroySegment(_segments[i]);
            _segments.Clear();

            var staleChildren = new List<GameObject>();
            foreach (Transform child in transform)
            {
                if (child != null && child.name.StartsWith("Segment_"))
                    staleChildren.Add(child.gameObject);
            }

            foreach (var child in staleChildren)
                Destroy(child);
        }

        private SegmentInstance CreateSegmentGO(int index, GameObject prefab)
        {
            // セグメント用Prefabを生成してMesh構成を集める
            // Instantiate module prefab and collect mesh deformers
            var segment = new SegmentInstance();
            var instance = Instantiate(prefab, transform);
            instance.layer = LayerConst.BlockLayer;
            instance.name = $"Segment_{index}";
            segment.Root = instance;
            PrepareMeshComponents(instance, segment);
            _segments.Add(segment);
            return segment;
        }

        private void PrepareMeshComponents(GameObject root, SegmentInstance segment)
        {
            // メッシュコンポーネントを収集して変形設定を行う
            // Collect mesh components and attach deformers
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null)
                    continue;

                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null)
                    renderer = filter.gameObject.AddComponent<MeshRenderer>();

                if (_useMeshCollider)
                    ConfigureMeshCollider(filter);

                var meshComponent = filter.GetComponent<BezierRailMesh>();
                if (meshComponent == null)
                    meshComponent = filter.gameObject.AddComponent<BezierRailMesh>();

                meshComponent.SetUseGpuDeform(_useGpuDeform);
                meshComponent.SetSourceMesh(filter.sharedMesh);
                segment.Meshes.Add(meshComponent);

                if (!_enableDeleteTarget)
                    continue;

                var deleteTarget = filter.GetComponent<DeleteTargetRail>();
                if (deleteTarget == null)
                    deleteTarget = filter.gameObject.AddComponent<DeleteTargetRail>();

                deleteTarget.SetParentBezierRailChain(this);
                deleteTarget.SetRailGraphCache(_railGraphClientCache);
            }
        }

        private void ConfigureSegmentInstance(SegmentInstance segment, float offset, float length, int curveSamples)
        {
            // 変形に必要なパラメータを設定する
            // Apply deformation parameters
            foreach (var mesh in segment.Meshes)
            {
                mesh.SetControlPoints(_point0, _point1, _point2, _point3);
                mesh.SetAxes(_forwardAxis, _upAxis);
                mesh.SetSamples(curveSamples);
                mesh.SetCurveData(_curveLength, _arcLengths, curveSamples);
                if (_usePreviewColor)
                    mesh.SetPreviewColor(_previewColor);
                mesh.ConfigureSegment(offset, length);
                mesh.Deform();
            }
        }

        private void TryCreatePartialSegment(ref int remainderSteps, int stepValue, GameObject prefab, float segmentLength, ref float offset, int curveSamples)
        {
            if (remainderSteps < stepValue)
                return;
            if (prefab == null)
                return;

            var segment = CreateSegmentGO(_segments.Count, prefab);
            ConfigureSegmentInstance(segment, offset, segmentLength, curveSamples);
            offset += segmentLength;
            remainderSteps -= stepValue;
        }

        private float GetModuleLength(GameObject prefab, float fallback)
        {
            // モジュール長をMeshから推定する
            // Measure prefab span via child meshes
            if (prefab == null)
                return fallback;

            float prefabLength = 0f;
            var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null)
                    continue;
                var candidate = BezierRailMesh.CalculateModuleLength(filter.sharedMesh, _forwardAxis, _upAxis);
                if (candidate > prefabLength)
                    prefabLength = candidate;
            }

            return prefabLength > 1e-4f ? prefabLength : fallback;
        }

        private static void DestroySegment(SegmentInstance segment)
        {
            if (segment?.Root == null)
                return;
            Destroy(segment.Root);
        }

        private void ConfigureMeshCollider(MeshFilter filter)
        {
            var collider = filter.GetComponent<MeshCollider>();
            if (collider == null)
                collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
        }

        private int ResolveCurveSamples()
        {
            var maxSamples = _useGpuDeform ? BezierRailMesh.MaxCurveSamples : 1024;
            return Mathf.Clamp(_curveSamples, 8, maxSamples);
        }

        private void ApplyDeformModeToMeshes()
        {
            for (var i = 0; i < _segments.Count; i++)
            {
                var segment = _segments[i];
                for (var j = 0; j < segment.Meshes.Count; j++)
                    segment.Meshes[j].SetUseGpuDeform(_useGpuDeform);
            }
        }

        private void ApplyPreviewColorToMeshes()
        {
            // プレビュー色を既存メッシュに反映する
            // Apply preview color to existing meshes
            for (var i = 0; i < _segments.Count; i++)
            {
                var segment = _segments[i];
                for (var j = 0; j < segment.Meshes.Count; j++)
                    segment.Meshes[j].SetPreviewColor(_previewColor);
            }
        }

        private sealed class SegmentInstance
        {
            internal GameObject Root;
            internal readonly List<BezierRailMesh> Meshes = new();
        }

        public void SetRemovePreviewing()
        {
            if (_controller == null)
                return;

            _controller.CopyAndSetMaterial(_removeMaterial);
            _controller.SetColor(MaterialConst.PreviewColorPropertyName, MaterialConst.NotPlaceableColor);
        }

        public void ResetMaterial()
        {
            if (_controller == null)
                return;

            _controller.ResetMaterial();
        }
    }
}
