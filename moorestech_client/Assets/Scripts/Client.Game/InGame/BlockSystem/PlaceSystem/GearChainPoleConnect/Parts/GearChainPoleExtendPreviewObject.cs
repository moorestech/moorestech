using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts
{
    /// <summary>
    /// 歯車チェーンポール延長のゴーストブロックと接続線の表示。
    /// PositionGhostは入力フェーズの環境クエリ（配置と地面判定）、Applyは出力フェーズの表示反映で何も返さない。
    /// Ghost pole block and connection line view for gear chain pole extension.
    /// PositionGhost is the input-phase environment query (placement and ground detect); Apply is the output-phase render that returns nothing.
    /// </summary>
    public class GearChainPoleExtendPreviewObject
    {
        private const float LineWidth = 0.05f;
        private const float LineSpacing = 0.1f;

        private readonly IPlacementPreviewBlockGameObjectController _ghostController;
        private readonly List<PlaceInfo> _positionedPlaceInfos = new();

        private GameObject _lineRoot;
        private LineRenderer _lineRenderer1;
        private LineRenderer _lineRenderer2;

        public GearChainPoleExtendPreviewObject(IPlacementPreviewBlockGameObjectController ghostController)
        {
            _ghostController = ghostController;
        }

        /// <summary>
        /// ゴーストを配置して地面クリアかを返す入力フェーズのクエリ。表示可否の最終判定はApplyで行う
        /// Input-phase query that positions the ghost and returns ground clearance. Final visibility is applied by Apply
        /// </summary>
        public bool PositionGhost(PlaceInfo placeInfo, BlockMasterElement poleBlockMaster)
        {
            _positionedPlaceInfos.Clear();
            _positionedPlaceInfos.Add(placeInfo);

            // 地面判定はゴーストの物理接触を読むため、配置と有効化が必要
            // Ground detect reads the ghost's physics contact, so it must be positioned and activated
            _ghostController.SetActive(true);
            var groundOverlapList = _ghostController.SetPreviewAndGroundDetect(_positionedPlaceInfos, poleBlockMaster);
            return !groundOverlapList[0];
        }

        /// <summary>
        /// プレビュー表示指示を反映する。GhostVisibleは同フレームでのPositionGhost呼び出しが前提
        /// Apply the preview command. GhostVisible assumes PositionGhost was called in the same frame
        /// </summary>
        public void Apply(GearChainPolePreviewCommand command)
        {
            ApplyGhost();
            ApplyLine();

            #region Internal

            void ApplyGhost()
            {
                if (!command.GhostVisible || _positionedPlaceInfos.Count == 0)
                {
                    _ghostController.SetActive(false);
                    return;
                }

                // 最終判定色でゴーストを塗り直す
                // Repaint the ghost with the final judgement color
                _positionedPlaceInfos[0].Placeable = command.GhostPlaceable;
                _ghostController.UpdatePlaceableColors(_positionedPlaceInfos);
            }

            void ApplyLine()
            {
                if (!command.LineVisible)
                {
                    if (_lineRoot != null) _lineRoot.SetActive(false);
                    return;
                }

                EnsureLines();
                _lineRoot.SetActive(true);

                // 2本のラインを水平にオフセットして描画
                // Draw two horizontally offset lines like the existing chain view
                var direction = (command.LineEnd - command.LineStart).normalized;
                var right = Vector3.Cross(Vector3.up, direction).normalized;
                if (right == Vector3.zero) right = Vector3.right;
                var offset = right * (LineSpacing / 2f);

                var color = command.LinePlaceable ? MaterialConst.PlaceableColor : MaterialConst.NotPlaceableColor;
                SetLine(_lineRenderer1, command.LineStart + offset, command.LineEnd + offset, color);
                SetLine(_lineRenderer2, command.LineStart - offset, command.LineEnd - offset, color);
            }

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

            void SetLine(LineRenderer lineRenderer, Vector3 lineStart, Vector3 lineEnd, Color color)
            {
                lineRenderer.SetPosition(0, lineStart);
                lineRenderer.SetPosition(1, lineEnd);
                lineRenderer.startColor = color;
                lineRenderer.endColor = color;
            }

            #endregion
        }

        /// <summary>
        /// 有効化・無効化時のリセット用に全表示を隠す
        /// Hide everything for reset on enable/disable
        /// </summary>
        public void Hide()
        {
            Apply(GearChainPolePreviewCommand.Hidden);
        }
    }
}
