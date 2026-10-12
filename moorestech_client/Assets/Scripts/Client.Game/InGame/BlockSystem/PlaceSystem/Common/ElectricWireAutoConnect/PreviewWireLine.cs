using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect
{
    /// <summary>
    ///     通常設置とBPで共有する電線プレビュー
    ///     Wire preview shared by normal placement and blueprints
    /// </summary>
    internal class PreviewWireLine
    {
        private readonly GameObject _gameObject;
        private readonly MeshFilter _meshFilter;
        private readonly Material _material;
        private Mesh _mesh;

        // 直前の端点を保持して不要な再構築を避ける
        // Cache the last endpoints to avoid needless rebuilds
        private Vector3 _cachedStart;
        private Vector3 _cachedEnd;
        private bool _hasCache;

        internal PreviewWireLine(Transform parent)
        {
            _gameObject = new GameObject("PreviewWireLine");
            _gameObject.transform.SetParent(parent, false);
            _meshFilter = _gameObject.AddComponent<MeshFilter>();
            var renderer = _gameObject.AddComponent<MeshRenderer>();

            // 複製材質を半透明可否色で初期化
            // Initialize cloned material with translucent judgement colors.
            _material = new Material(MaterialConst.GetPreviewPlaceBlockMaterial());
            SetColor(false);
            renderer.sharedMaterial = _material;
        }

        internal void SetActive(bool active)
        {
            _gameObject.SetActive(active);
        }

        // 可否に応じてワイヤー線の色を切り替える
        // Switch the wire line's color by placeability
        internal void SetColor(bool isFailure)
        {
            var color = isFailure ? MaterialConst.NotPlaceableColor : MaterialConst.PlaceableColor;
            color.a = 0.5f;
            _material.SetColor(MaterialConst.PreviewColorPropertyName, color);
            _material.color = color;
        }

        internal void Draw(Vector3 start, Vector3 end)
        {
            _gameObject.SetActive(true);

            // 端点が変わらなければメッシュは再構築しない
            // Skip mesh rebuild when the endpoints are unchanged
            if (_hasCache && _cachedStart == start && _cachedEnd == end) return;

            var newMesh = CatenaryWireMeshBuilder.Build(start, end, new List<(Vector3, Vector3, float)>());
            if (_mesh != null) Object.Destroy(_mesh);
            _mesh = newMesh;
            _meshFilter.mesh = _mesh;

            _cachedStart = start;
            _cachedEnd = end;
            _hasCache = true;
        }
    }
}
