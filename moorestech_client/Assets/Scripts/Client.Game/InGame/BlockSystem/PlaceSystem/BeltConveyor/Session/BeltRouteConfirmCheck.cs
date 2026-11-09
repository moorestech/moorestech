using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session.Branch;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session
{
    /// <summary>
    ///     確定の瞬間に読み直した世界で経路がまだ置けるか確かめる
    ///     Checks at the confirm instant that the route is still placeable in the re-read world
    ///     設置中は世界の変化を追わないので、ここが唯一の検査になる（ADR 0099）
    ///     World changes are not followed during placement, so this is the only check (ADR 0099)
    /// </summary>
    public static class BeltRouteConfirmCheck
    {
        public static bool IsStillPlaceable(IBeltRouteWorld world, BlockGameObjectDataStore blocks, BeltRouteSession session, BeltConveyorHoldingBlock holding, bool useAlternateExit, List<PlaceInfo> infos)
        {
            if (session.IsBranch && !IsBranchStillUsable()) return false;

            // 分岐器のセル以外は全て空きセルのままであること
            // Every cell except the splitter's must still be free
            foreach (var info in infos)
            {
                if (session.IsBranch && info.Position == session.Branch.Cell) continue;
                if (BeltRouteCellJudge.Judge(world, info.Position) != BeltRouteCellBlockReason.None) return false;
            }
            return true;

            #region Internal

            // 同じ個体でも隣接変化で分岐器の向き・出口が変わっていたら使えない
            // Even for the same belt, a changed splitter facing or exit makes the branch unusable
            bool IsBranchStillUsable()
            {
                var branch = session.Branch;
                if (!blocks.TryGetBlockGameObject(branch.Cell, out var current) || !current.BlockInstanceId.Equals(branch.Belt.BlockInstanceId)) return false;
                var reason = BeltRouteBranchOrigin.Judge(blocks, current, holding, useAlternateExit, out var rejudged);
                return reason == BeltRouteBranchBlockReason.None && rejudged.SplitterFacing == branch.SplitterFacing && rejudged.ExitDirection == branch.ExitDirection;
            }

            #endregion
        }
    }
}
