using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    ///     通常ブロック設置のプレビュー投入とカーソル理由集約を1手にまとめる（ADR 0047）
    ///     Bundles normal block placement's preview submission and cursor reason reporting into one step (ADR 0047)
    ///     地形との重なりは設置不可の理由にしない。ここが地形を見ない唯一の設置系
    ///     Terrain overlap never blocks placement here; this is the only terrain-blind placement system
    /// </summary>
    public static class NormalPlacementPreviewStep
    {
        public static int Apply(IPlacementPreviewBlockGameObjectController previewController, List<PlaceInfo> placeInfos, IReadOnlyList<PlacementBlockCause> cellCauses, Vector3Int cursorCell, BlockMasterElement holdingBlockMaster, PlacementFeedback feedback)
        {
            // DetectGroundOverlapsを呼ばないことが仕様。呼ぶとADR 0047が撤回される
            // Not calling DetectGroundOverlaps is the specification; calling it would revoke ADR 0047
            previewController.SetPreview(placeInfos, holdingBlockMaster);

            return PlacementCellReasonReporter.ResolveCursorAndReportCauses(placeInfos, cellCauses, cursorCell, feedback);
        }
    }
}
