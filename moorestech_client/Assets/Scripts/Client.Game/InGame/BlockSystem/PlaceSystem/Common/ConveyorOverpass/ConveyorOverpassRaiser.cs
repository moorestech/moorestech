using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass
{
    // 障害物スキャン→包絡線→PlaceInfo列にY上昇・縦方向再計算・設置可否を反映する
    // Scan obstacles -> envelope -> apply raised Y, recompute vertical direction, and mark placeability on the PlaceInfo list.
    public class ConveyorOverpassRaiser
    {
        private readonly ConveyorObstacleScanner _scanner = new();

        public void Raise(List<PlaceInfo> placeInfos, int cornerIndex, Func<Vector3Int, bool> isOccupied)
        {
            if (placeInfos.Count == 0) return;

            // 障害物下限から最終ベルト高さプロファイルと端点可否を求める
            // Compute the final belt-height profile and endpoint feasibility from the obstacle lower bounds.
            var cells = placeInfos.Select(info => info.Position).ToList();
            var lowerBounds = _scanner.ComputeLowerBounds(cells, isOccupied);
            var (beltY, feasible) = ConveyorVerticalEnvelope.Solve(lowerBounds, cells[0].y, cells[^1].y, cornerIndex);

            // 上昇したセルとその隣接のみ縦方向を再計算する（無関係なセルの既存方向は維持）
            // Recompute vertical direction only for raised cells and their neighbors (untouched cells keep their existing direction).
            var raised = new bool[placeInfos.Count];
            for (var i = 0; i < placeInfos.Count; i++) raised[i] = beltY[i] != cells[i].y;

            for (var i = 0; i < placeInfos.Count; i++)
            {
                var info = placeInfos[i];
                var pos = info.Position;
                pos.y = beltY[i];
                info.Position = pos;

                if (NeighborhoodChanged(i)) info.VerticalDirection = ResolveVertical(i);
                if (!feasible[i]) info.Placeable = false;
            }

            #region Internal

            bool NeighborhoodChanged(int i)
            {
                if (raised[i]) return true;
                if (i > 0 && raised[i - 1]) return true;
                if (i + 1 < raised.Length && raised[i + 1]) return true;
                return false;
            }

            BlockVerticalDirection ResolveVertical(int i)
            {
                // 次が高ければ登り、前が高ければ下り、それ以外は水平
                // Up if the next cell is higher, Down if the previous is higher, otherwise Horizontal.
                if (i + 1 < beltY.Length && beltY[i + 1] > beltY[i]) return BlockVerticalDirection.Up;
                if (i - 1 >= 0 && beltY[i - 1] > beltY[i]) return BlockVerticalDirection.Down;
                return BlockVerticalDirection.Horizontal;
            }

            #endregion
        }
    }
}
