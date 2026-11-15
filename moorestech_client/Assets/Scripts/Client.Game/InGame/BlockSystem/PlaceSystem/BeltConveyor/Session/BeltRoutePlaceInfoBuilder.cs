using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session.Branch;
using Core.Master;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session
{
    /// <summary>
    ///     経路をサーバーへ送る設置情報へ写す。分岐なら先頭に分岐器を足す
    ///     Maps a route to the placement infos sent to the server, prepending the splitter for a branch
    /// </summary>
    public static class BeltRoutePlaceInfoBuilder
    {
        public static List<PlaceInfo> Build(BeltRoutePlan plan, BeltConveyorHoldingBlock holding, BeltRouteBranchOrigin branch)
        {
            var infos = new List<PlaceInfo>(plan.Cells.Count + 1);
            if (branch != null) infos.Add(SplitterInfo(branch, true));
            foreach (var cell in plan.Cells)
            {
                infos.Add(new PlaceInfo
                {
                    Position = cell.Cell,
                    Direction = cell.Facing,
                    VerticalDirection = ToVertical(cell.Kind),
                    BlockId = holding.ResolveBlockId(cell.Kind),
                    Placeable = true,
                });
            }
            return infos;
        }

        // 経路なしのとき始点（分岐なら分岐器）と終点のゴーストだけを赤で出す
        // With no route, only the start (the splitter for a branch) and end ghosts are shown in red
        public static List<PlaceInfo> BuildEndpointGhosts(BeltRouteSession session, Vector3Int endCell, BlockDirection endDirection, BeltConveyorHoldingBlock holding)
        {
            var start = session.IsBranch ? SplitterInfo(session.Branch, false) : Single(session.StartCell, session.StartDirection, holding.BlockId, false);
            return new List<PlaceInfo> { start, Single(endCell, endDirection, holding.BlockId, false) };
        }

        public static PlaceInfo Single(Vector3Int cell, BlockDirection direction, BlockId blockId, bool placeable)
        {
            return new PlaceInfo
            {
                Position = cell,
                Direction = direction,
                VerticalDirection = BlockVerticalDirection.Horizontal,
                BlockId = blockId,
                Placeable = placeable,
            };
        }

        // 分岐器と、選んだ出口の先に出る最初のセルを候補として見せる
        // Shows the splitter and the first cell beyond the chosen exit as the candidate
        public static List<PlaceInfo> BuildBranchCandidate(BeltRouteBranchOrigin branch, BeltConveyorHoldingBlock holding)
        {
            var firstCell = branch.Cell + BeltRouteDirection.Forward(branch.ExitDirection);
            return new List<PlaceInfo> { SplitterInfo(branch, true), Single(firstCell, branch.ExitDirection, holding.BlockId, true) };
        }

        public static PlaceInfo SplitterInfo(BeltRouteBranchOrigin branch, bool placeable)
        {
            return Single(branch.Cell, branch.SplitterFacing, branch.SplitterBlockId, placeable);
        }

        private static BlockVerticalDirection ToVertical(BeltRouteCellKind kind)
        {
            return kind switch
            {
                BeltRouteCellKind.Up => BlockVerticalDirection.Up,
                BeltRouteCellKind.Down => BlockVerticalDirection.Down,
                _ => BlockVerticalDirection.Horizontal,
            };
        }
    }
}
