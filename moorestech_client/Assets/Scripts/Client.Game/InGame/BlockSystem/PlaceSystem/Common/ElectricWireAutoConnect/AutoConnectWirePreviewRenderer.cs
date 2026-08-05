using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect
{
    /// <summary>
    /// 設置プレビュー中に自動接続される複数ワイヤーと合計消費電線数を半透明で描画する
    /// Renders multiple auto-connect wires and total wire cost semi-transparently during placement preview
    /// </summary>
    public class AutoConnectWirePreviewRenderer
    {
        // 端点・カテナリーとも実描画（ElectricWireLineViewElement）と同一計算
        // Endpoints and catenary match the actual rendering (ElectricWireLineViewElement)
        private const float SagRatio = 0.1f;
        private const float WireAlpha = 0.5f;
        private const float CostLabelFontSize = 3f;
        private static readonly Vector3 CostLabelOffset = new(0f, 0.8f, 0f);

        private readonly Camera _mainCamera;
        private readonly Transform _root;
        private readonly List<WireLine> _wireLines = new();
        private readonly TextMeshPro _costLabel;

        public AutoConnectWirePreviewRenderer(Camera mainCamera)
        {
            _mainCamera = mainCamera;

            // 線とラベルの親を構築
            // Build a parent GameObject grouping wire lines and the label
            var rootObject = new GameObject("AutoConnectWirePreview");
            _root = rootObject.transform;

            // 合計コストのラベルを子生成
            // Create a world-space total wire cost label as a child
            var labelObject = new GameObject("AutoConnectWireCostLabel");
            labelObject.transform.SetParent(_root, false);
            _costLabel = labelObject.AddComponent<TextMeshPro>();
            _costLabel.fontSize = CostLabelFontSize;
            _costLabel.alignment = TextAlignmentOptions.Center;

            _root.gameObject.SetActive(false);
        }

        /// <summary>
        /// 起点端点から各接続先端点へワイヤーを張り、合計消費電線数を表示する
        /// Draws wires from the origin endpoint to each target endpoint and shows the total wire cost
        /// </summary>
        public void ShowCost(Vector3 originEndpoint, IReadOnlyList<Vector3> targetEndpoints, int totalWireCost)
        {
            DrawWires(originEndpoint, targetEndpoints, false);
            if (totalWireCost <= 0)
            {
                _costLabel.gameObject.SetActive(false);
                return;
            }
            PlaceLabel(originEndpoint, $"電線 x{totalWireCost}", false);
        }

        /// <summary>
        /// 設置を拒否した理由を不可色で表示する。線は拒否時も参考として描画する
        /// Shows the rejection reason in the failure color; the wires stay drawn for reference
        /// </summary>
        public void ShowFailure(Vector3 originEndpoint, IReadOnlyList<Vector3> targetEndpoints, string reasonText)
        {
            DrawWires(originEndpoint, targetEndpoints, true);
            PlaceLabel(originEndpoint, reasonText, true);
        }

        /// <summary>
        /// 設置は許可したまま、配線が起きない事情を情報色で案内する
        /// Keeps placement allowed while explaining in the info color why no wire is drawn
        /// </summary>
        public void ShowNotice(Vector3 originEndpoint, IReadOnlyList<Vector3> targetEndpoints, string noticeText)
        {
            DrawWires(originEndpoint, targetEndpoints, false);
            PlaceLabel(originEndpoint, noticeText, false);
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

        // ラベルを起点上へ配置しカメラへ向ける。可否で色を切り替える
        // Place the label above the origin, billboarded to the camera, colored by failure state
        private void PlaceLabel(Vector3 originEndpoint, string text, bool isFailure)
        {
            _costLabel.gameObject.SetActive(true);
            _costLabel.text = text;
            _costLabel.color = WithAlpha(isFailure ? MaterialConst.NotPlaceableColor : MaterialConst.PlaceableColor);

            var labelTransform = _costLabel.transform;
            labelTransform.position = originEndpoint + CostLabelOffset;
            labelTransform.rotation = Quaternion.LookRotation(labelTransform.position - _mainCamera.transform.position);
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

                var sag = Vector3.Distance(start, end) * SagRatio;
                var newMesh = CatenaryWireMeshBuilder.Build(start, end, sag, new List<(Vector3, Vector3, float)>());
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
