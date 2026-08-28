using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Ground
{
    /// <summary>
    ///     ドラッグ列のYを地形へ追従させる。追従してよいかの判断もこの型へ集める
    ///     Makes a drag run's Y follow the terrain; the decision of whether to follow is collected here too
    /// </summary>
    public class PlacementGroundFollowStep : IPlacementGroundFollower
    {
        public void FollowGround(PlacementRun run, PlacementHitSurfaceKind surfaceKind, Vector3Int blockSize, int heightOffset)
        {
            // ブロック面ヒット（積み重ね）は整数グリッド上なので触らない
            // Block-face hits (stacking) sit on the integer grid and stay untouched
            if (surfaceKind != PlacementHitSurfaceKind.Ground) return;

            // Y軸へ伸びた列は各セルのYが積み上げ段数そのものなので、地形へ揃えると全セルが1セルへ潰れる
            // A run extended along Y carries the stack level in each cell's Y, so aligning them to the terrain collapses the run into one cell
            if (run.Axis == PlacementRunAxis.Y) return;

            for (var i = 0; i < run.Cells.Count; i++)
            {
                var cell = run.Cells[i];
                if (PlacementGroundCellResolver.TryResolveCellFromGround(cell.Position, cell.Direction, blockSize, heightOffset, out var resolvedPosition))
                {
                    cell.Position = resolvedPosition;
                    continue;
                }

                // 地表が取れないセルは埋まりを避けられないため設置不可にし、理由を表示側へ渡す
                // A cell with no ground cannot avoid being buried, so it is blocked and the reason is handed to the display side
                cell.Placeable = false;
                run.BlockCauses[i] = PlacementBlockCause.GroundNotFound;
            }
        }
    }
}
