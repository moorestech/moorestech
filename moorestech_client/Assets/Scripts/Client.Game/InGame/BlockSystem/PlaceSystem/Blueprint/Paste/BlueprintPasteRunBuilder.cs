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
        public static List<BlueprintPasteOrigin> BuildOrigins(Vector3Int startOrigin, Vector3Int cursorOrigin, Vector3Int footprintSize, PlacementHitSurfaceKind surfaceKind, int heightOffset, out int cursorIndex)
        {
            var run = PlacementRunPositionCalculator.Calculate(startOrigin, cursorOrigin, footprintSize);
            cursorIndex = run.CursorIndex;

            // 水平な地面列は各外接箱の底面内の最高点へ追従する
            // Horizontal ground runs follow the terrain maximum under each extent
            var followsGround = surfaceKind == PlacementHitSurfaceKind.Ground && run.Axis != PlacementRunAxis.Y;
            var origins = new List<BlueprintPasteOrigin>(run.Positions.Count);
            foreach (var position in run.Positions)
            {
                if (!followsGround)
                {
                    origins.Add(new BlueprintPasteOrigin(position, true));
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
