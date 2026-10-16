using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    ///     通常設置プレビューと理由集約（ADR 0081）
    ///     Bundles normal placement previews with terrain sinks and cursor reasons (ADR 0081)
    /// </summary>
    public static class NormalPlacementPreviewStep
    {
        public static int Apply(IPlacementPreviewBlockGameObjectController previewController, List<PlaceInfo> placeInfos, IReadOnlyList<PlacementBlockCause> cellCauses, Vector3Int cursorCell, BlockMasterElement holdingBlockMaster, PlacementFeedback feedback)
        {
            previewController.SetPreview(placeInfos, holdingBlockMaster);
            var terrainSinks = PlacementTerrainSinkQuery.DetectSunkCells(placeInfos);
            return PlacementCellReasonReporter.ApplyTerrainSinksAndReport(placeInfos, cellCauses, cursorCell, terrainSinks, feedback);
        }
    }
}
