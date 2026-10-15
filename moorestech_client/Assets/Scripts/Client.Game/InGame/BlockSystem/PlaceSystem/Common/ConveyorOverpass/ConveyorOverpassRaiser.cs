using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass
{
    // 地形と障害物の下限を包絡線で解きPlaceInfo列へ反映する
    // Solve terrain and obstacle lower bounds with the envelope and apply them to PlaceInfo
    public class ConveyorOverpassRaiser
    {
        private readonly ConveyorObstacleScanner _scanner = new();

        public ConveyorOverpassRaiseResult Raise(List<PlaceInfo> placeInfos, int cornerIndex, Func<Vector3Int, bool> isOccupied, IConveyorTerrainFloor terrainFloor)
        {
            var overpassBlocked = new bool[placeInfos.Count];
            if (placeInfos.Count == 0) return new ConveyorOverpassRaiseResult(overpassBlocked, false);

            // 地形で持ち上げた基準から解き、端点へ戻れなければ地形なしで解き直す
            // Solve from the terrain-lifted base, then retry without terrain if endpoints cannot be reached
            var cells = placeInfos.Select(info => info.Position).ToList();
            var terrainLiftedCells = LiftByTerrain();
            var (beltY, feasible) = SolveFrom(terrainLiftedCells);
            var terrainClimbAbandoned = false;
            if (!EndpointsReached(feasible) && !terrainLiftedCells.SequenceEqual(cells))
            {
                (beltY, feasible) = SolveFrom(cells);
                // 地形なしでも解けない場合は不可理由を優先して案内を出さない
                // If the terrain-free solve still fails, show the block reason instead of the notice
                terrainClimbAbandoned = EndpointsReached(feasible);
            }

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

                // 端点を固定できず立体交差が組めないセル（セル自体は空でも置けない）
                // Cells where the endpoints cannot be pinned, so no overpass fits (unplaceable even on an empty cell)
                if (info.Placeable && !feasible[i])
                {
                    info.Placeable = false;
                    overpassBlocked[i] = true;
                }
            }

            return new ConveyorOverpassRaiseResult(overpassBlocked, terrainClimbAbandoned);

            #region Internal

            List<Vector3Int> LiftByTerrain()
            {
                // 端点を固定し、谷では下げず丘だけ持ち上げる
                // Keep endpoints fixed and lift only hills, never lower for valleys
                var lifted = new List<Vector3Int>(cells);
                for (var i = 1; i < lifted.Count - 1; i++)
                {
                    // silent-ok: 地表が無いセルは下限を付けない(ADR 0081) / プレビューでそのセルが持ち上がらないことが見える
                    if (!terrainFloor.TryGetFloorY(lifted[i], out var floorY)) continue;
                    if (floorY <= lifted[i].y) continue;
                    lifted[i] = new Vector3Int(lifted[i].x, floorY, lifted[i].z);
                }
                return lifted;
            }

            bool EndpointsReached(bool[] solvedFeasible)
            {
                // 包絡線が両端の固定高さへ戻れたかを判定する
                // Check whether the envelope reached both fixed endpoint heights
                return !solvedFeasible.Contains(false);
            }

            (int[] beltY, bool[] feasible) SolveFrom(List<Vector3Int> baseCells)
            {
                // 持ち上げ後の基準から既存ブロックを上へ走査する
                // Scan existing blocks upward from the lifted base
                var lowerBounds = _scanner.ComputeLowerBounds(baseCells, isOccupied);
                return ConveyorVerticalEnvelope.Solve(lowerBounds, cells[0].y, cells[^1].y, cornerIndex);
            }

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
