using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session.Branch;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session
{
    /// <summary>
    ///     確定の瞬間に読み直した世界で経路がまだ置けるか確かめる
    ///     Checks at the confirm instant that the route is still placeable in the re-read world
    ///     設置中は世界の変化を追わないので、ここが唯一の検査になる（ADR 0100）
    ///     World changes are not followed during placement, so this is the only check (ADR 0100)
    /// </summary>
    public static class BeltRouteConfirmCheck
    {
        public static bool IsStillPlaceable(IBeltRouteWorld world, BlockGameObjectDataStore blocks, BeltRouteSession session, BeltConveyorHoldingBlock holding, bool useAlternateExit, List<PlaceInfo> infos, List<PlaceInfo> replannedInfos)
        {
            // 置けなければ送らずに始点選びへ戻るので、開発者向けに理由をログへ出す
            // A failure sends nothing and returns to start selection, so log it for developers
            var placeable = (!session.IsBranch || IsBranchStillUsable()) && AreCellsStillFree() && IsSameRouteWhenReplanned();
            if (!placeable) Debug.Log($"[BeltRoute] world changed before confirm; back to start selection start:{session.StartCell} end:{infos[infos.Count - 1].Position} branch:{session.IsBranch}");
            return placeable;

            #region Internal

            // 分岐器のセル以外は全て空きセルのままであること
            // Every cell except the splitter's must still be free
            bool AreCellsStillFree()
            {
                foreach (var info in infos)
                {
                    if (session.IsBranch && info.Position == session.Branch.Cell) continue;
                    if (BeltRouteCellJudge.Judge(world, info.Position) != BeltRouteCellBlockReason.None) return false;
                }
                return true;
            }

            // 世界が作り直されていたら（null以外）、探索し直した経路が同じセル・向きであること
            // When the world was rebuilt (non-null), the re-searched route must have the same cells and facings
            bool IsSameRouteWhenReplanned()
            {
                if (replannedInfos == null) return true;
                if (replannedInfos.Count != infos.Count) return false;
                for (var i = 0; i < infos.Count; i++)
                {
                    if (replannedInfos[i].Position != infos[i].Position || replannedInfos[i].Direction != infos[i].Direction || replannedInfos[i].VerticalDirection != infos[i].VerticalDirection) return false;
                }
                return true;
            }

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
