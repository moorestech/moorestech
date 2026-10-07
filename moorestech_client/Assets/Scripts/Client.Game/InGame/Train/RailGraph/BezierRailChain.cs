using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Block;
using Cysharp.Threading.Tasks;
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
        [SerializeField] private bool _useMeshCollider = true;

        private Vector3 _point0 = new(0f, 0f, 0f);
        private Vector3 _point1 = new(0f, 0f, 2f);
        private Vector3 _point2 = new(0f, 0f, 4f);
        private Vector3 _point3 = new(0f, 0f, 6f);
        [SerializeField] private Vector3 _forwardAxis = Vector3.forward;
        [SerializeField] private Vector3 _upAxis = Vector3.up;
        private int _curveSamples = 64;
        private int _curveSampleCountCache = 64;
        private float _curveLength;
        private float[] _arcLengths;
        private bool _useGpuDeform;
        private Color _previewColor = MaterialConst.PlaceableColor;

        private readonly BezierRailChainSegments _segments = new();
        private readonly RailChainRemoveMaterial _removePreview = new();
        
        private RailGraphClientCache _railGraphClientCache;
        private RendererShaderAnimation _rendererShaderAnimation;
        
        public bool IsRemoving { get; private set; }
        
        public void SetRailGraphCache(RailGraphClientCache cache)
        {
            _railGraphClientCache = cache;
        }

        // GPU変形の使用を設定する
        // Configure GPU deform usage
        public void SetUseGpuDeform(bool enable)
        {
            _useGpuDeform = enable;
            _segments.SetUseGpuDeform(_useGpuDeform);
        }

        // プレビュー色を設定する
        // Set preview color
        public void SetPreviewColor(Color color)
        {
            _previewColor = color;
            _segments.SetPreviewColor(_previewColor);
        }
        
        public async UniTask PlaceAnimation()
        {
            _rendererShaderAnimation ??= gameObject.AddComponent<RendererShaderAnimation>();
            await _rendererShaderAnimation.PlaceAnimation();
        }
        
        public async UniTask RemoveAnimation()
        {
            IsRemoving = true;
            _rendererShaderAnimation ??= gameObject.AddComponent<RendererShaderAnimation>();
            await _rendererShaderAnimation.RemoveAnimation();
        }

        // MeshColliderの使用可否を設定する
        // Configure MeshCollider usage
        public void SetUseMeshCollider(bool useMeshCollider)
        {
            _useMeshCollider = useMeshCollider;
            _segments.SetUseMeshCollider(_useMeshCollider);
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
            _segments.Clear(transform);
            _removePreview.Invalidate();
            if (_modulePrefab == null)
            {
                Debug.LogWarning("[BezierRailChain] rebuild skipped: module prefab missing");
                return;
            }

            var moduleLength = _segments.GetModuleLength(_modulePrefab, _forwardAxis, _upAxis, 0f);
            if (moduleLength <= 0f)
            {
                Debug.LogWarning("[BezierRailChain] rebuild skipped: module length unavailable");
                return;
            }

            _curveSampleCountCache = _useGpuDeform ? Mathf.Clamp(_curveSamples, 8, BezierRailMesh.MaxCurveSamples) : _curveSamples;
            _curveLength = BezierUtility.BuildArcLengthTable(_point0, _point1, _point2, _point3, _curveSampleCountCache, ref _arcLengths);
            if (_curveLength <= 1e-4f)
            {
                Debug.LogWarning("[BezierRailChain] rebuild skipped: curve too short");
                return;
            }

            // メッシュ生成へ現在の曲線設定を渡す
            // Pass current curve settings to mesh creation
            _segments.Configure(new BezierRailChainSegments.Settings
            {
                Owner = this,
                RailCache = _railGraphClientCache,
                ForwardAxis = _forwardAxis,
                UpAxis = _upAxis,
                Point0 = _point0,
                Point1 = _point1,
                Point2 = _point2,
                Point3 = _point3,
                CurveSamples = _curveSamples,
                CurveSampleCount = _curveSampleCountCache,
                CurveLength = _curveLength,
                ArcLengths = _arcLengths,
                UseGpuDeform = _useGpuDeform,
                UseMeshCollider = _useMeshCollider,
                PreviewColor = _previewColor,
            });
            var offset = 0f;
            var fullSegmentCount = Mathf.Max(0, Mathf.FloorToInt(_curveLength / moduleLength));

            for (var i = 0; i < fullSegmentCount; i++)
            {
                var segment = _segments.CreateSegmentGO(i, _modulePrefab);
                _segments.ConfigureSegmentInstance(segment, offset, moduleLength);
                offset += moduleLength;
            }

            var remainder = Mathf.Max(0f, _curveLength - offset);
            if (remainder > 1e-4f)
            {
                // 端数は多めに生成し、終端Clampによる縮小で隙間を吸収する
                // Generate extra remainder coverage and let end clamping shrink the last segment
                _segments.FillRemainder(remainder, moduleLength, offset, _halfModulePrefab, _quarterModulePrefab, _eighthModulePrefab);
            }

            if (!_useGpuDeform) _removePreview.Capture(gameObject);
        }

        private void OnDestroy()
        {
            _segments.Clear(transform);
        }

        private void OnDisable()
        {
            _segments.Clear(transform);
            _removePreview.Invalidate();
        }

        public void SetRemovePreviewing()
        {
            if (IsRemoving)
            {
                Debug.Log("[BezierRailChain] preview skipped: rail is removing");
                return;
            }
            _removePreview.SetRed(gameObject, _useGpuDeform, _segments);
        }
        
        public void ResetMaterial()
        {
            _removePreview.Reset(_useGpuDeform, _previewColor, _segments);
        }
    }
}
