using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session
{
    /// <summary>
    ///     始点確定後の1フレーム分の判断。カーソルのセルを終点として経路を求め、見せる設置情報と可否を返す
    ///     One frame's decision after the start is fixed: routes to the cursor cell as the end and returns the infos to show with their placeability
    /// </summary>
    public static class BeltRouteRoutingFrame
    {
        public static List<PlaceInfo> Resolve(BeltRouteSession session, BeltRouteCursorHit hit, BeltConveyorHoldingBlock holding, PlacementFeedback feedback, out bool placeable)
        {
            var endCell = hit.Cell;
            var endDirection = hit.Direction;

            // 始点のセルをもう一度指すとベルト1個。向きと可否は終点と同じ判定
            // Pointing at the start cell again places a single belt; facing and placeability follow the end's rules
            if (!session.IsBranch && endCell == session.StartCell)
            {
                placeable = true;
                return new List<PlaceInfo> { BeltRoutePlaceInfoBuilder.Single(endCell, endDirection, holding.BlockId, true) };
            }

            var result = session.Plan(endCell, endDirection, holding);
            if (!result.HasPlan)
            {
                var line = result.EndCellBlockReason != BeltRouteCellBlockReason.None ? BeltRouteTooltipKey.CellLine(result.EndCellBlockReason) : BeltRouteTooltipKey.NoPathLine();
                feedback.Add(line);
                placeable = false;
                return BeltRoutePlaceInfoBuilder.BuildEndpointGhosts(session, endCell, endDirection, holding);
            }

            placeable = true;
            return BeltRoutePlaceInfoBuilder.Build(result.Plan, holding, session.Branch);
        }

        // 確定時に読み直した世界での経路。経路が無ければ空のリスト
        // The route in the world re-read at confirm; an empty list when there is no route
        public static List<PlaceInfo> ReplanForConfirm(BeltRouteSession session, BeltRouteCursorHit hit, BeltConveyorHoldingBlock holding)
        {
            if (!session.IsBranch && hit.Cell == session.StartCell) return new List<PlaceInfo> { BeltRoutePlaceInfoBuilder.Single(hit.Cell, hit.Direction, holding.BlockId, true) };

            var result = session.PlanFresh(hit.Cell, hit.Direction, holding);
            return result.HasPlan ? BeltRoutePlaceInfoBuilder.Build(result.Plan, holding, session.Branch) : new List<PlaceInfo>();
        }
    }
}
