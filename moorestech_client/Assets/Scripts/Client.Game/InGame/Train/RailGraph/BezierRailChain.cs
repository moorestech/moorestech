using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Block;
using Game.Train.RailCalc;
using UnityEngine;

/// <summary>
/// 1本のベジエ曲線上にFBXレールモジュールを並べる親オブジェクト
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
        
        private void Awake()
        {
            _removeMaterial = Resources.Load<Material>(MaterialConst.PreviewPlaceBlockMaterial);
        }
        
        public void SetRailGraphCache(RailGraphClientCache cache)
        {
            _railGraphClientCache = cache;
        }
        
        /// <summary>外部コードから制御点を再設定する</summary>
        public void SetControlPoints(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            _point0 = p0;
            _point1 = p1;
            _point2 = p2;
            _point3 = p3;
        }

        public void Rebuild()
        {
            // ベジエチェーンを最新情報で組み直す
            // Rebuild full chain along the current control points
            ClearSegmentsImmediate();
            if (_modulePrefab == null)
                return;

            var moduleLength = GetModuleLength(_modulePrefab, 0f);
            if (moduleLength <= 0f)
                return;

            var curveLength = BezierUtility.GetBezierCurveLength(_point0, _point1, _point2, _point3, _curveSamples);
            if (curveLength <= 1e-4f)
                return;

            var offset = 0f;
            var fullSegmentCount = Mathf.Max(0, Mathf.FloorToInt(curveLength / moduleLength));

            for (var i = 0; i < fullSegmentCount; i++)
            {
                var segment = CreateSegmentGO(i, _modulePrefab);
                ConfigureSegmentInstance(segment, offset, moduleLength);
                offset += moduleLength;
            }

            var remainder = Mathf.Max(0f, curveLength - offset);
            if (remainder <= 1e-4f)
                return;

            var remainderSteps = Mathf.Clamp(Mathf.RoundToInt(remainder / moduleLength * 8f), 1, 7);
            var halfLength = _halfModulePrefab != null ? GetModuleLength(_halfModulePrefab, moduleLength * 0.5f) : moduleLength * 0.5f;
            var quarterLength = _quarterModulePrefab != null ? GetModuleLength(_quarterModulePrefab, moduleLength * 0.25f) : moduleLength * 0.25f;
            var eighthLength = _eighthModulePrefab != null ? GetModuleLength(_eighthModulePrefab, moduleLength * 0.125f) : moduleLength * 0.125f;
            TryCreatePartialSegment(ref remainderSteps, 4, _halfModulePrefab, halfLength, ref offset);
            TryCreatePartialSegment(ref remainderSteps, 2, _quarterModulePrefab, quarterLength, ref offset);
            TryCreatePartialSegment(ref remainderSteps, 1, _eighthModulePrefab, eighthLength, ref offset);
            if (remainderSteps > 0)
            {
                Debug.LogWarning($"[BezierRailChain] 端数を埋められませんでした (残りステップ:{remainderSteps}). 必要な長さのモジュールが揃っているか確認してください。", this);
            }
            
            _controller = new RendererMaterialReplacerController(gameObject);
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
            // 生成済みセグメントをすべて破棄
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
            // レール用プレハブをインスタンス化しBezier変形対象を収集
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
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null)
                    continue;

                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null)
                    renderer = filter.gameObject.AddComponent<MeshRenderer>();

                var collider = filter.GetComponent<MeshCollider>();
                if (collider == null)
                    collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;

                var meshComponent = filter.GetComponent<BezierRailMesh>();
                if (meshComponent == null)
                    meshComponent = filter.gameObject.AddComponent<BezierRailMesh>();
                
                var deleteTarget = filter.GetComponent<DeleteTargetRail>();
                if (deleteTarget == null)
                    deleteTarget = filter.gameObject.AddComponent<DeleteTargetRail>();
                
                meshComponent.SetSourceMesh(filter.sharedMesh);
                meshComponent.SetControlPoints(_point0, _point1, _point2, _point3);
                meshComponent.SetAxes(_forwardAxis, _upAxis);
                meshComponent.SetSamples(_curveSamples);
                deleteTarget.SetParentBezierRailChain(this);
                deleteTarget.SetRailGraphCache(_railGraphClientCache);
                meshComponent.Deform();
                segment.Meshes.Add(meshComponent);
            }
        }

        private void ConfigureSegmentInstance(SegmentInstance segment, float offset, float length)
        {
            foreach (var mesh in segment.Meshes)
            {
                mesh.SetControlPoints(_point0, _point1, _point2, _point3);
                mesh.SetAxes(_forwardAxis, _upAxis);
                mesh.SetSamples(_curveSamples);
                mesh.ConfigureSegment(offset, length);
                mesh.Deform();
            }
        }

        private void TryCreatePartialSegment(ref int remainderSteps, int stepValue, GameObject prefab, float segmentLength, ref float offset)
        {
            if (remainderSteps < stepValue)
                return;
            if (prefab == null)
                return;

            var segment = CreateSegmentGO(_segments.Count, prefab);
            ConfigureSegmentInstance(segment, offset, segmentLength);
            offset += segmentLength;
            remainderSteps -= stepValue;
        }

        private float GetModuleLength(GameObject prefab, float fallback)
        {
            // プレハブ配下のメッシュ群から長さを計測し、失敗時はフォールバック
            // Measure prefab span via child meshes and fall back when unavailable
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

        private sealed class SegmentInstance
        {
            internal GameObject Root;
            internal readonly List<BezierRailMesh> Meshes = new();
        }
        
        public void SetRemovePreviewing()
        {
            _controller.CopyAndSetMaterial(_removeMaterial);
            _controller.SetColor(MaterialConst.PreviewColorPropertyName, MaterialConst.NotPlaceableColor);
        }
        
        public void ResetMaterial()
        {
            _controller.ResetMaterial();   
        }
    }
}
