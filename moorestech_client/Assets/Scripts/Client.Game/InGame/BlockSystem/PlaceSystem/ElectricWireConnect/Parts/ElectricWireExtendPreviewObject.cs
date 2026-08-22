using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts
{
    /// <summary>
    /// 起点-接続先間のワイヤーを可否色で生成表示
    /// Runtime wire between origin and target, colored by placeability
    /// </summary>
    public class ElectricWireExtendPreviewObject
    {
        // 端点はElectricWireEndpointResolver、垂れ量はCatenaryWireMeshBuilder.Buildが内部で決め、実描画と同一計算になる
        // Endpoints come from ElectricWireEndpointResolver and the sag is decided inside CatenaryWireMeshBuilder.Build, matching the actual rendering
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
            // プレビュー用GameObjectを構築
            // Build a dedicated preview GameObject with mesh-rendering components
            _gameObject = new GameObject("ElectricWireExtendPreview");
            _meshFilter = _gameObject.AddComponent<MeshFilter>();
            var renderer = _gameObject.AddComponent<MeshRenderer>();

            // プレビュー材質を複製し青赤切替
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
        /// 端点からワイヤーを可否色で表示
        /// Shows the wire from endpoints, colored by placeability
        /// </summary>
        public void Show(Vector3 startWorldPos, Vector3 endWorldPos, bool placeable)
        {
            _gameObject.SetActive(true);

            // 変化が無ければメッシュは再構築しない
            // Skip mesh rebuild when nothing changed
            if (_hasCache && _cachedStart == startWorldPos && _cachedEnd == endWorldPos && _cachedPlaceable == placeable) return;

            // メッシュ再生成し可否色を設定
            // Rebuild the catenary mesh and set color by placeability
            var newMesh = CatenaryWireMeshBuilder.Build(startWorldPos, endWorldPos, new List<(Vector3, Vector3, float)>());
            if (_mesh != null) Object.Destroy(_mesh);
            _mesh = newMesh;
            _meshFilter.mesh = _mesh;

            var color = placeable ? MaterialConst.PlaceableColor : MaterialConst.NotPlaceableColor;
            _material.SetColor(MaterialConst.PreviewColorPropertyName, color);
            _material.color = color;

            _cachedStart = startWorldPos;
            _cachedEnd = endWorldPos;
            _cachedPlaceable = placeable;
            _hasCache = true;
        }
    }
}
