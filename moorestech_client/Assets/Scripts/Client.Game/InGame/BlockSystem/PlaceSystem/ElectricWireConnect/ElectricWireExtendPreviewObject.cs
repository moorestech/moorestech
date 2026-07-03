using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect
{
    /// <summary>
    /// 起点と接続先を結ぶプレビュー用ワイヤーをランタイム生成し、可否色と消費電線数を表示する
    /// Runtime-built preview wire connecting origin and target, showing placeability color and wire cost
    /// </summary>
    public class ElectricWireExtendPreviewObject
    {
        // 描画設定は本描画（Task10）と揃えて見た目を一致させる
        // Match the actual rendering (Task 10) so the preview looks consistent
        private const float SagRatio = 0.1f;
        private const float CostLabelFontSize = 3f;
        private static readonly Vector3 BlockCenterOffset = new(0.5f, 0.5f, 0.5f);
        private static readonly Vector3 CostLabelOffset = new(0f, 0.5f, 0f);

        private readonly Camera _mainCamera;
        private readonly GameObject _gameObject;
        private readonly MeshFilter _meshFilter;
        private readonly Material _material;
        private readonly TextMeshPro _costLabel;
        private Mesh _mesh;

        // 直前の描画パラメータを保持して不要な再構築を避ける
        // Cache the last draw parameters to avoid needless rebuilds
        private Vector3 _cachedStart;
        private Vector3 _cachedEnd;
        private bool _cachedPlaceable;
        private bool _hasCache;

        public ElectricWireExtendPreviewObject(Camera mainCamera)
        {
            _mainCamera = mainCamera;

            // プレビュー用GameObjectを構築
            // Build a dedicated preview GameObject with mesh-rendering components
            _gameObject = new GameObject("ElectricWireExtendPreview");
            _meshFilter = _gameObject.AddComponent<MeshFilter>();
            var renderer = _gameObject.AddComponent<MeshRenderer>();

            // プレビュー材質を複製し青赤切替
            // Clone the shared preview material and switch blue/red via _PreviewColor
            _material = new Material(MaterialConst.GetPreviewPlaceBlockMaterial());
            renderer.sharedMaterial = _material;

            // 消費電線数のワールド空間ラベルを子として生成する
            // Create a world-space wire cost label as a child
            var labelObject = new GameObject("WireCostLabel");
            labelObject.transform.SetParent(_gameObject.transform, false);
            _costLabel = labelObject.AddComponent<TextMeshPro>();
            _costLabel.fontSize = CostLabelFontSize;
            _costLabel.alignment = TextAlignmentOptions.Center;

            _gameObject.SetActive(false);
        }

        public void SetActive(bool active)
        {
            _gameObject.SetActive(active);
            if (!active) _hasCache = false;
        }

        /// <summary>
        /// 両端ブロック座標（原点）からワイヤープレビューと消費電線数を表示する
        /// Show the wire preview and wire cost from both endpoint block positions (origins)
        /// </summary>
        public void Show(Vector3Int fromBlockPos, Vector3Int toBlockPos, bool placeable, int wireCostCount)
        {
            var start = fromBlockPos + BlockCenterOffset;
            var end = toBlockPos + BlockCenterOffset;

            _gameObject.SetActive(true);
            UpdateCostLabel();

            // 変化が無ければメッシュは再構築しない
            // Skip mesh rebuild when nothing changed
            if (_hasCache && _cachedStart == start && _cachedEnd == end && _cachedPlaceable == placeable) return;

            // メッシュ再生成し可否色を設定
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

            #region Internal

            // 消費電線数ラベルをワイヤー中点に置き、カメラへ向けて可否色と同期させる
            // Place the wire cost label at the wire midpoint, billboard it to the camera and sync its color
            void UpdateCostLabel()
            {
                if (wireCostCount <= 0)
                {
                    _costLabel.gameObject.SetActive(false);
                    return;
                }

                _costLabel.gameObject.SetActive(true);
                _costLabel.text = $"電線 x{wireCostCount}";
                _costLabel.color = placeable ? MaterialConst.PlaceableColor : MaterialConst.NotPlaceableColor;

                var labelTransform = _costLabel.transform;
                labelTransform.position = (start + end) * 0.5f + CostLabelOffset;
                labelTransform.rotation = Quaternion.LookRotation(labelTransform.position - _mainCamera.transform.position);
            }

            #endregion
        }
    }
}
