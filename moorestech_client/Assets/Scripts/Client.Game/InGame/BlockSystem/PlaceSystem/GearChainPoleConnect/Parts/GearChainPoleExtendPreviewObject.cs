using System.Collections.Generic;
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
        private readonly IPlacementPreviewBlockGameObjectController _ghostController;
        private readonly List<PlaceInfo> _positionedPlaceInfos = new();
        private GearChainPreviewLine _line;

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
            _ghostController.SetPreview(_positionedPlaceInfos, poleBlockMaster);
            var groundOverlapList = _ghostController.DetectGroundOverlaps();
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
                    _line?.SetActive(false);
                    return;
                }

                // BPと通常延長で同じチェーン描画を使う
                // Share chain rendering with blueprint previews
                _line ??= new GearChainPreviewLine(null);
                _line.Draw(command.LineStart, command.LineEnd, command.LinePlaceable);
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
