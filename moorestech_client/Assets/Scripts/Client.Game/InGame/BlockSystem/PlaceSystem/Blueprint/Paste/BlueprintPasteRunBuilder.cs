using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Game.Block.Interface;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste
{
    /// <summary>
    ///     外接箱の列を生成し地形へ追従する
    ///     Builds blueprint extent runs and follows the ground
    /// </summary>
    public static class BlueprintPasteRunBuilder
    {
        public static List<BlueprintPasteOrigin> BuildOrigins(Vector3Int startAnchor, Vector3Int cursorAnchor, Vector3Int placementStart, Vector3Int footprintSize, PlacementHitSurfaceKind surfaceKind, int heightOffset, out int cursorIndex)
        {
            var run = PlacementRunPositionCalculator.Calculate(startAnchor, cursorAnchor, footprintSize);
            cursorIndex = run.CursorIndex;

            // 水平地面列は底面内の最高点に追従
            // Follow the footprint terrain maximum for horizontal ground runs.
            var followsGround = surfaceKind == PlacementHitSurfaceKind.Ground && run.Axis != PlacementRunAxis.Y;
            var origins = new List<BlueprintPasteOrigin>(run.Positions.Count);
            foreach (var position in run.Positions)
            {
                if (!followsGround)
                {
                    // 軸と本数はヒット段で決め、縦列へ始点の地形補正を戻す
                    // Determine axis and count from hit anchors, then restore the starting terrain adjustment
                    var placement = run.Axis == PlacementRunAxis.Y ? position + (placementStart - startAnchor) : position;
                    origins.Add(new BlueprintPasteOrigin(placement, true));
                    continue;
                }

                // 寸法は回転済みなのでNorthで探査し、地形欠損は共有判定へ伝える
                // Probe the already rotated size as North and pass missing-ground status to shared planning
                var found = PlacementGroundCellResolver.TryResolveCellFromGround(position, BlockDirection.North, footprintSize, heightOffset, out var resolved);
                origins.Add(new BlueprintPasteOrigin(found ? resolved : position, found));
            }

            return origins;
        }
    }
}
