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
        // 描画設定は本描画（Task10）と揃えて見た目を一致させる
        // Match the actual rendering (Task 10) so the preview looks consistent
        private const float SagRatio = 0.1f;
        private const float WireAlpha = 0.5f;
        private const float CostLabelFontSize = 3f;
        private static readonly Vector3 BlockCenterOffset = new(0.5f, 0.5f, 0.5f);
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
        /// 起点ブロックから各接続先ブロックへワイヤーを張り、合計消費電線数を表示する
        /// Draws wires from the origin block to each target block and shows the total wire cost
        /// </summary>
        public void Show(Vector3Int originBlockPos, IReadOnlyList<Vector3Int> targetBlockPositions, int totalWireCost)
        {
            _root.gameObject.SetActive(true);
            var origin = originBlockPos + BlockCenterOffset;

            // 必要数のワイヤー線を確保し、各ターゲットへカテナリーを張る
            // Ensure enough wire lines and draw a catenary to each target
            EnsureWireLineCount(targetBlockPositions.Count);
            for (var i = 0; i < _wireLines.Count; i++)
            {
                if (targetBlockPositions.Count <= i)
                {
                    _wireLines[i].SetActive(false);
                    continue;
                }

                var end = targetBlockPositions[i] + BlockCenterOffset;
                _wireLines[i].Draw(origin, end);
            }

            UpdateCostLabel();

            #region Internal

            // 合計消費電線数ラベルを起点上に置き、カメラへ向ける
            // Place the total wire cost label above the origin and billboard it to the camera
            void UpdateCostLabel()
            {
                if (totalWireCost <= 0)
                {
                    _costLabel.gameObject.SetActive(false);
                    return;
                }

                _costLabel.gameObject.SetActive(true);
                _costLabel.text = $"電線 x{totalWireCost}";
                _costLabel.color = WithAlpha(MaterialConst.PlaceableColor);

                var labelTransform = _costLabel.transform;
                labelTransform.position = origin + CostLabelOffset;
                labelTransform.rotation = Quaternion.LookRotation(labelTransform.position - _mainCamera.transform.position);
            }

            void EnsureWireLineCount(int count)
            {
                while (_wireLines.Count < count) _wireLines.Add(new WireLine(_root));
            }

            #endregion
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

                // 材質を複製し半透明接続色で固定
                // Clone the shared preview material and fix it to the semi-transparent placeable color
                var material = new Material(MaterialConst.GetPreviewPlaceBlockMaterial());
                var color = WithAlpha(MaterialConst.PlaceableColor);
                material.SetColor(MaterialConst.PreviewColorPropertyName, color);
                material.color = color;
                renderer.sharedMaterial = material;
            }

            public void SetActive(bool active)
            {
                _gameObject.SetActive(active);
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
