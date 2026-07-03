using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect
{
    /// <summary>
    /// 起点と接続先を結ぶプレビュー用ワイヤーをランタイム生成し、可否で色分け表示する
    /// Runtime-built preview wire connecting origin and target, colored by placeability
    /// </summary>
    public class ElectricWireExtendPreviewObject
    {
        // 描画設定は本描画（Task10）と揃えて見た目を一致させる
        // Match the actual rendering (Task 10) so the preview looks consistent
        private const float SagRatio = 0.1f;
        private static readonly Vector3 BlockCenterOffset = new(0.5f, 0.5f, 0.5f);

        private readonly GameObject _gameObject;
        private readonly MeshFilter _meshFilter;
        private readonly Material _material;
        private Mesh _mesh;

        // 直前の描画パラメータを保持して不要な再構築を避ける
        // Cache the last draw parameters to avoid needless rebuilds
        private Vector3 _cachedStart;
        private Vector3 _cachedEnd;
        private bool _cachedPlaceable;
        private bool _hasCache;

        public ElectricWireExtendPreviewObject()
        {
            // プレビュー専用のGameObjectとメッシュ描画コンポーネントを構築する
            // Build a dedicated preview GameObject with mesh-rendering components
            _gameObject = new GameObject("ElectricWireExtendPreview");
            _meshFilter = _gameObject.AddComponent<MeshFilter>();
            var renderer = _gameObject.AddComponent<MeshRenderer>();

            // 共通プレビュー材質を複製し、_PreviewColorで青赤を切り替える
            // Clone the shared preview material and switch blue/red via _PreviewColor
            _material = new Material(MaterialConst.GetPreviewPlaceBlockMaterial());
            renderer.sharedMaterial = _material;

            _gameObject.SetActive(false);
        }

        public void SetActive(bool active)
        {
            _gameObject.SetActive(active);
            if (!active) _hasCache = false;
        }

        /// <summary>
        /// 両端ブロック座標（原点）からワイヤープレビューを表示する
        /// Show the wire preview from both endpoint block positions (origins)
        /// </summary>
        public void Show(Vector3Int fromBlockPos, Vector3Int toBlockPos, bool placeable)
        {
            var start = fromBlockPos + BlockCenterOffset;
            var end = toBlockPos + BlockCenterOffset;

            _gameObject.SetActive(true);

            // 変化が無ければ再構築しない
            // Skip rebuild when nothing changed
            if (_hasCache && _cachedStart == start && _cachedEnd == end && _cachedPlaceable == placeable) return;

            // カテナリーメッシュを再生成し、可否に応じて色を設定する
            // Rebuild the catenary mesh and set color by placeability
            var sag = Vector3.Distance(start, end) * SagRatio;
            var newMesh = CatenaryWireMeshBuilder.Build(start, end, sag, new List<(Vector3, Vector3, float)>());
            if (_mesh != null) Object.Destroy(_mesh);
            _mesh = newMesh;
            _meshFilter.mesh = _mesh;

            var color = placeable ? MaterialConst.PlaceableColor : MaterialConst.NotPlaceableColor;
            _material.SetColor(MaterialConst.PreviewColorPropertyName, color);
            _material.color = color;

            _cachedStart = start;
            _cachedEnd = end;
            _cachedPlaceable = placeable;
            _hasCache = true;
        }
    }
}
