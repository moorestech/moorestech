using System.Collections.Generic;
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

        private readonly Transform _root;
        private readonly List<PreviewWireLine> _wireLines = new();

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

            while (_wireLines.Count < targetEndpoints.Count) _wireLines.Add(new PreviewWireLine(_root));
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

    }
}
