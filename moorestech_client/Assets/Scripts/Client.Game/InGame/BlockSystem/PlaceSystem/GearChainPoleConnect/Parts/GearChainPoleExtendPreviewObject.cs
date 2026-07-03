using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts
{
    /// <summary>
    /// 歯車チェーンポール延長のゴーストブロックと接続線を表示する。
    /// 接続線は既存のチェーン表示と異なり任意のワールド座標を受け取れる。
    /// Displays the ghost pole block and connection line for gear chain pole extension.
    /// Unlike the existing chain view, the line accepts arbitrary world positions.
    /// </summary>
    public class GearChainPoleExtendPreviewObject
    {
        private const float LineWidth = 0.05f;
        private const float LineSpacing = 0.1f;

        private readonly IPlacementPreviewBlockGameObjectController _ghostController;

        private GameObject _lineRoot;
        private LineRenderer _lineRenderer1;
        private LineRenderer _lineRenderer2;

        public GearChainPoleExtendPreviewObject(IPlacementPreviewBlockGameObjectController ghostController)
        {
            _ghostController = ghostController;
        }

        /// <summary>
        /// ゴーストブロックを表示し、地面判定込みの最終的な設置可否を返す
        /// Show the ghost block and return final placeability including ground detection
        /// </summary>
        public bool ShowGhost(PlaceInfo placeInfo, BlockMasterElement poleBlockMaster, bool isPlaceable)
        {
            var placeInfos = new List<PlaceInfo> { placeInfo };

            // ゴーストを配置しつつ地面との衝突を取得する（衝突していたら設置不可）
            // Place the ghost and get ground collision (colliding means not placeable)
            _ghostController.SetActive(true);
            var groundOverlapList = _ghostController.SetPreviewAndGroundDetect(placeInfos, poleBlockMaster);
            var isGroundClear = !groundOverlapList[0];

            // 最終判定で色分けを更新する
            // Update the color with the final judgement
            placeInfo.Placeable = isPlaceable && isGroundClear;
            _ghostController.UpdatePlaceableColors(placeInfos);

            return placeInfo.Placeable;
        }

        /// <summary>
        /// 接続線を判定色つきで表示する
        /// Show the connection line colored by judgement
        /// </summary>
        public void ShowLine(Vector3 start, Vector3 end, bool isPlaceable)
        {
            EnsureLines();
            _lineRoot.SetActive(true);

            // 2本のラインを水平にオフセットして描画
            // Draw two horizontally offset lines like the existing chain view
            var direction = (end - start).normalized;
            var right = Vector3.Cross(Vector3.up, direction).normalized;
            if (right == Vector3.zero) right = Vector3.right;
            var offset = right * (LineSpacing / 2f);

            var color = isPlaceable ? MaterialConst.PlaceableColor : MaterialConst.NotPlaceableColor;
            ApplyLine(_lineRenderer1, start + offset, end + offset, color);
            ApplyLine(_lineRenderer2, start - offset, end - offset, color);

            #region Internal

            void EnsureLines()
            {
                if (_lineRoot != null) return;

                // プレビュー用ラインを実行時に生成する（シーン配線を不要にするため）
                // Create preview lines at runtime (avoids scene wiring)
                _lineRoot = new GameObject("GearChainPoleExtendPreviewLine");
                _lineRenderer1 = CreateLineRenderer("Line1");
                _lineRenderer2 = CreateLineRenderer("Line2");
            }

            LineRenderer CreateLineRenderer(string lineName)
            {
                var lineObject = new GameObject(lineName);
                lineObject.transform.SetParent(_lineRoot.transform);

                var lineRenderer = lineObject.AddComponent<LineRenderer>();
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                lineRenderer.startWidth = LineWidth;
                lineRenderer.endWidth = LineWidth;
                lineRenderer.positionCount = 2;
                return lineRenderer;
            }

            void ApplyLine(LineRenderer lineRenderer, Vector3 lineStart, Vector3 lineEnd, Color color)
            {
                lineRenderer.SetPosition(0, lineStart);
                lineRenderer.SetPosition(1, lineEnd);
                lineRenderer.startColor = color;
                lineRenderer.endColor = color;
            }

            #endregion
        }

        public void HideGhost()
        {
            _ghostController.SetActive(false);
        }

        public void HideLine()
        {
            if (_lineRoot != null) _lineRoot.SetActive(false);
        }

        public void Hide()
        {
            HideGhost();
            HideLine();
        }
    }
}
