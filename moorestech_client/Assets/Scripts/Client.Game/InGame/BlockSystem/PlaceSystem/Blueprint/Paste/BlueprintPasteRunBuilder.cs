using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Game.Block.Interface;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste
{
    public static class BlueprintPasteRunBuilder
    {
        public static List<BlueprintPasteOrigin> BuildOrigins(Vector3Int startOrigin, Vector3Int cursorOrigin, Vector3Int footprintSize, PlacementHitSurfaceKind surfaceKind, int heightOffset)
        {
            var run = PlacementRunPositionCalculator.Calculate(startOrigin, cursorOrigin, footprintSize);

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

                // 地形欠損は原点に残し共有判定へ伝える
                // Preserve missing-ground status on the origin for shared planning
                var found = PlacementGroundCellResolver.TryResolveCellFromGround(position, BlockDirection.North, footprintSize, heightOffset, out var resolved);
                if (!found) Debug.LogWarning($"[BlueprintPaste] Ground not found under origin {position}, footprint {footprintSize}.");
                origins.Add(new BlueprintPasteOrigin(found ? resolved : position, found));
            }

            return origins;
        }
    }
}
