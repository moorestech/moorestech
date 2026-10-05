using System.Collections.Generic;
using Client.Common;
using UnityEngine;

namespace Client.Game.InGame.Train.RailGraph
{
    // 1本の曲線に沿うメッシュ生成と破棄を担当する
    // Owns mesh creation and disposal along one curve
    public sealed class BezierRailChainSegments
    {
        public struct Settings
        {
            public BezierRailChain Owner;
            public RailGraphClientCache RailCache;
            public Vector3 Point0;
            public Vector3 Point1;
            public Vector3 Point2;
            public Vector3 Point3;
            public Vector3 ForwardAxis;
            public Vector3 UpAxis;
            public int CurveSamples;
            public int CurveSampleCount;
            public float CurveLength;
            public float[] ArcLengths;
            public bool UseGpuDeform;
            public bool UseMeshCollider;
            public Color PreviewColor;
        }

        public sealed class SegmentInstance
        {
            public GameObject Root;
            public readonly List<BezierRailMesh> Meshes = new();
        }

        private readonly List<SegmentInstance> _segments = new();
        private Settings _settings;

        public void Configure(Settings settings)
        {
            _settings = settings;
        }

        public void SetUseGpuDeform(bool enabled)
        {
            foreach (var segment in _segments)
                foreach (var mesh in segment.Meshes) mesh.SetUseGpuDeform(enabled);
        }

        public void SetPreviewColor(Color color)
        {
            foreach (var segment in _segments)
                foreach (var mesh in segment.Meshes) mesh.SetPreviewColor(color);
        }

        public void SetUseMeshCollider(bool enabled)
        {
            foreach (var segment in _segments)
            {
                if (segment.Root == null) continue;
                foreach (var collider in segment.Root.GetComponentsInChildren<MeshCollider>(true)) collider.enabled = enabled;
            }
        }

        public void Clear(Transform parent)
        {
            // 生成済みと残存する旧セグメントを破棄する
            // Destroy tracked segments and any stale children
            foreach (var segment in _segments)
                if (segment.Root != null) Object.Destroy(segment.Root);
            _segments.Clear();
            var staleChildren = new List<GameObject>();
            foreach (Transform child in parent)
                if (child != null && child.name.StartsWith("Segment_")) staleChildren.Add(child.gameObject);
            foreach (var child in staleChildren) Object.Destroy(child);
        }

        public SegmentInstance CreateSegmentGO(int index, GameObject prefab)
        {
            // プレハブを生成し、変形対象のメッシュを集める
            // Instantiate the prefab and collect its deformable meshes
            var segment = new SegmentInstance();
            var instance = Object.Instantiate(prefab, _settings.Owner.transform);
            instance.layer = LayerConst.BlockLayer;
            instance.name = $"Segment_{index}";
            segment.Root = instance;
            PrepareMeshComponents(instance, segment);
            _segments.Add(segment);
            return segment;
        }

        public void ConfigureSegmentInstance(SegmentInstance segment, float offset, float length)
        {
            foreach (var mesh in segment.Meshes)
            {
                mesh.SetControlPoints(_settings.Point0, _settings.Point1, _settings.Point2, _settings.Point3);
                mesh.SetAxes(_settings.ForwardAxis, _settings.UpAxis);
                mesh.SetSamples(_settings.CurveSamples);
                mesh.SetUseGpuDeform(_settings.UseGpuDeform);
                mesh.SetCurveData(_settings.CurveLength, _settings.ArcLengths, _settings.CurveSampleCount);
                mesh.SetPreviewColor(_settings.PreviewColor);
                mesh.ConfigureSegment(offset, length);
                mesh.Deform();
            }
        }

        public void TryCreatePartialSegment(ref int remainderSteps, int stepValue, GameObject prefab, float segmentLength, ref float offset)
        {
            if (remainderSteps < stepValue || prefab == null) return;
            var segment = CreateSegmentGO(_segments.Count, prefab);
            ConfigureSegmentInstance(segment, offset, segmentLength);
            offset += segmentLength;
            remainderSteps -= stepValue;
        }

        public float GetModuleLength(GameObject prefab, Vector3 forwardAxis, Vector3 upAxis, float fallback)
        {
            if (prefab == null) return fallback;
            var prefabLength = 0f;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var candidate = BezierRailMesh.CalculateModuleLength(filter.sharedMesh, forwardAxis, upAxis);
                if (candidate > prefabLength) prefabLength = candidate;
            }
            return prefabLength > 1e-4f ? prefabLength : fallback;
        }

        private void PrepareMeshComponents(GameObject root, SegmentInstance segment)
        {
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null) filter.gameObject.AddComponent<MeshRenderer>();
                ConfigureMeshCollider(filter);
                var mesh = filter.GetComponent<BezierRailMesh>();
                if (mesh == null) mesh = filter.gameObject.AddComponent<BezierRailMesh>();
                var deleteTarget = filter.GetComponent<DeleteTargetRail>();
                if (deleteTarget == null) deleteTarget = filter.gameObject.AddComponent<DeleteTargetRail>();

                mesh.SetSourceMesh(filter.sharedMesh);
                mesh.SetAxes(_settings.ForwardAxis, _settings.UpAxis);
                mesh.SetSamples(_settings.CurveSamples);
                mesh.SetUseGpuDeform(_settings.UseGpuDeform);
                mesh.SetPreviewColor(_settings.PreviewColor);
                deleteTarget.SetParentBezierRailChain(_settings.Owner);
                deleteTarget.SetRailGraphCache(_settings.RailCache);
                segment.Meshes.Add(mesh);
            }
        }

        private void ConfigureMeshCollider(MeshFilter filter)
        {
            var collider = filter.GetComponent<MeshCollider>();
            if (_settings.UseMeshCollider)
            {
                if (collider == null) collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.enabled = true;
                return;
            }
            if (collider != null) collider.enabled = false;
        }
    }
}
