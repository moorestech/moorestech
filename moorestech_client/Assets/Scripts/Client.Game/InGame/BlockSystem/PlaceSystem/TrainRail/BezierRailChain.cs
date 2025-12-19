using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1つのベジエ曲線上に3Dレールfbxを複製し敷き詰める。これは親
/// BezierRailMesh を複数子オブジェクトとして持つ。
/// </summary>
namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail
{
    public class BezierRailChain : MonoBehaviour
    {
        [SerializeField] private Mesh _moduleMesh;
        [SerializeField] private Mesh _halfModuleMesh;
        [SerializeField] private Mesh _quarterModuleMesh;
        [SerializeField] private Mesh _eighthModuleMesh;
        [SerializeField] private Material[] _materials;

        private Vector3 _point0 = new(0f, 0f, 0f);
        private Vector3 _point1 = new(0f, 0f, 2f);
        private Vector3 _point2 = new(0f, 0f, 4f);
        private Vector3 _point3 = new(0f, 0f, 6f);
        [SerializeField] private Vector3 _forwardAxis = Vector3.forward;
        [SerializeField] private Vector3 _upAxis = Vector3.up;
        private int _curveSamples = 64;

        private readonly List<BezierRailMesh> _segments = new();

        /// <summary>外部コードから制御点を設定し直す。</summary>
        public void SetControlPoints(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            _point0 = p0;
            _point1 = p1;
            _point2 = p2;
            _point3 = p3;
        }

        public void Rebuild()
        {
            ClearSegmentsImmediate();
            if (_moduleMesh == null)
                return;

            var moduleLength = BezierRailMesh.CalculateModuleLength(_moduleMesh, _forwardAxis, _upAxis);
            if (moduleLength <= 0f)
                return;

            var curveLength = BezierRailMesh.EstimateCurveLength(_point0, _point1, _point2, _point3, _curveSamples);
            if (curveLength <= 1e-4f)
                return;

            var sharedMaterials = ResolveMaterials();
            var offset = 0f;
            var fullSegmentCount = Mathf.Max(0, Mathf.FloorToInt(curveLength / moduleLength));

            for (var i = 0; i < fullSegmentCount; i++)
            {
                var segment = CreateSegmentGO(i, _moduleMesh, sharedMaterials);
                segment.ConfigureSegment(offset, moduleLength);
                offset += moduleLength;
            }

            var remainder = Mathf.Max(0f, curveLength - offset);
            if (remainder <= 1e-4f)
                return;

            var remainderSteps = Mathf.Clamp(Mathf.RoundToInt(remainder / moduleLength * 8f), 1, 7);
            var halfLength = GetModuleLength(_halfModuleMesh, moduleLength * 0.5f);
            var quarterLength = GetModuleLength(_quarterModuleMesh, moduleLength * 0.25f);
            var eighthLength = GetModuleLength(_eighthModuleMesh, moduleLength * 0.125f);
            TryCreatePartialSegment(ref remainderSteps, 4, _halfModuleMesh, halfLength, sharedMaterials, ref offset);
            TryCreatePartialSegment(ref remainderSteps, 2, _quarterModuleMesh, quarterLength, sharedMaterials, ref offset);
            TryCreatePartialSegment(ref remainderSteps, 1, _eighthModuleMesh, eighthLength, sharedMaterials, ref offset);
            if (remainderSteps > 0)
            {
                Debug.LogWarning($"[BezierRailChain] 端数を埋められませんでした (残りステップ:{remainderSteps}). 必要な長さのモジュールが揃っているか確認してください。", this);
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
            for (var i = _segments.Count - 1; i >= 0; i--)
            {
                if (_segments[i] == null)
                    continue;
                DestroySegment(_segments[i].gameObject);
            }

            _segments.Clear();

            // 念のため既存の Segment_* を全削除
            var children = new List<Transform>();
            foreach (Transform child in transform)
            {
                if (child != null && child.name.StartsWith("Segment_"))
                    children.Add(child);
            }

            foreach (var child in children)
                DestroySegment(child.gameObject);
        }

        private Material[] ResolveMaterials()
        {
            if (_materials != null && _materials.Length > 0)
                return _materials;
            if (TryGetComponent<MeshRenderer>(out var renderer) && renderer.sharedMaterials.Length > 0)
                return renderer.sharedMaterials;
            return null;
        }

        private BezierRailMesh CreateSegmentGO(int index, Mesh mesh, Material[] sharedMaterials)
        {
            var child = new GameObject($"Segment_{index}");
            child.transform.SetParent(transform, false);

            var meshFilter = child.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = child.AddComponent<MeshRenderer>();
            if (sharedMaterials != null && sharedMaterials.Length > 0)
                meshRenderer.sharedMaterials = sharedMaterials;

            child.AddComponent<MeshCollider>();

            var segment = child.AddComponent<BezierRailMesh>();
            segment.SetSourceMesh(mesh);
            segment.SetControlPoints(_point0, _point1, _point2, _point3);
            segment.SetAxes(_forwardAxis, _upAxis);
            segment.SetSamples(_curveSamples);
            segment.Deform();
            _segments.Add(segment);
            return segment;
        }

        private void TryCreatePartialSegment(ref int remainderSteps, int stepValue, Mesh mesh, float segmentLength, Material[] sharedMaterials, ref float offset)
        {
            if (remainderSteps < stepValue || mesh == null)
                return;
            var segment = CreateSegmentGO(_segments.Count, mesh, sharedMaterials);
            segment.ConfigureSegment(offset, segmentLength);
            offset += segmentLength;
            remainderSteps -= stepValue;
        }

        private float GetModuleLength(Mesh mesh, float fallback)
        {
            if (mesh == null)
                return fallback;
            var length = BezierRailMesh.CalculateModuleLength(mesh, _forwardAxis, _upAxis);
            return length > 1e-4f ? length : fallback;
        }

        private static void DestroySegment(GameObject go)
        {
            if (go == null)
                return;
            Destroy(go);
        }
    }
}