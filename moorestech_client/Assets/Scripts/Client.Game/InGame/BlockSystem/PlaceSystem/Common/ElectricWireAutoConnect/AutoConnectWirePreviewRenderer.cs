using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect
{
    /// <summary>
    /// 自動接続ワイヤーを半透明でプレビュー描画
    /// Renders auto-connect wires semi-transparently
    /// </summary>
    public class AutoConnectWirePreviewRenderer
    {
        // 端点はElectricWireEndpointResolver、垂れ量はCatenaryWireMeshBuilder.Buildが内部で決め、実描画と同一計算になる
        // Endpoints come from ElectricWireEndpointResolver and the sag is decided inside CatenaryWireMeshBuilder.Build, matching the actual rendering
        private const float WireAlpha = 0.5f;

        private readonly Transform _root;
        private readonly List<WireLine> _wireLines = new();

        public AutoConnectWirePreviewRenderer()
        {
            // 線の親を構築
            // Build a parent GameObject grouping wire lines
            var rootObject = new GameObject("AutoConnectWirePreview");
            _root = rootObject.transform;

            _root.gameObject.SetActive(false);
        }

        /// <summary>
        /// 起点から各接続先へワイヤー表示。文言はツールチップ側が持つ
        /// Draws wires from origin to each target; text lives in the tooltip
        /// </summary>
        public void Show(Vector3 originEndpoint, IReadOnlyList<Vector3> targetEndpoints, bool isFailure)
        {
            DrawWires(originEndpoint, targetEndpoints, isFailure);
        }

        // 必要数のワイヤー線を確保し、各ターゲットへ可否色でカテナリーを張る
        // Ensure enough wire lines and draw a catenary to each target colored by failure state
        private void DrawWires(Vector3 originEndpoint, IReadOnlyList<Vector3> targetEndpoints, bool isFailure)
        {
            _root.gameObject.SetActive(true);

            while (_wireLines.Count < targetEndpoints.Count) _wireLines.Add(new WireLine(_root));
            for (var i = 0; i < _wireLines.Count; i++)
            {
                if (targetEndpoints.Count <= i)
                {
                    _wireLines[i].SetActive(false);
                    continue;
                }

                _wireLines[i].SetColor(isFailure);
                _wireLines[i].Draw(originEndpoint, targetEndpoints[i]);
            }
        }

        public void Hide()
        {
            _root.gameObject.SetActive(false);
        }

        private static Color WithAlpha(Color color)
        {
            return new Color(color.r, color.g, color.b, WireAlpha);
        }

        // 単一ワイヤーのメッシュ描画単位。起点⇔対象のカテナリーを半透明で表示する
        // A single wire's mesh unit, showing the origin-to-target catenary semi-transparently
        private class WireLine
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

            public WireLine(Transform parent)
            {
                _gameObject = new GameObject("AutoConnectWire");
                _gameObject.transform.SetParent(parent, false);
                _meshFilter = _gameObject.AddComponent<MeshFilter>();
                var renderer = _gameObject.AddComponent<MeshRenderer>();

                // 材質を複製し半透明接続色で初期化（可否色はSetColorで都度切り替える）
                // Clone the shared preview material with the semi-transparent placeable color (SetColor switches it per-call)
                _material = new Material(MaterialConst.GetPreviewPlaceBlockMaterial());
                SetColor(false);
                renderer.sharedMaterial = _material;
            }

            public void SetActive(bool active)
            {
                _gameObject.SetActive(active);
            }

            // 可否に応じてワイヤー線の色を切り替える
            // Switch the wire line's color by placeability
            public void SetColor(bool isFailure)
            {
                var color = WithAlpha(isFailure ? MaterialConst.NotPlaceableColor : MaterialConst.PlaceableColor);
                _material.SetColor(MaterialConst.PreviewColorPropertyName, color);
                _material.color = color;
            }

            public void Draw(Vector3 start, Vector3 end)
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
}
