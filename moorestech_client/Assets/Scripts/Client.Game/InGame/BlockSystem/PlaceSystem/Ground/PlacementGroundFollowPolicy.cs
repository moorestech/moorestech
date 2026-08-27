using Client.Game.InGame.BlockSystem.PlaceSystem.Common;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Ground
{
    /// <summary>
    ///     設置セルを地形へ追従させてよい条件を決める
    ///     Decides when a placement cell may follow the terrain
    /// </summary>
    public static class PlacementGroundFollowPolicy
    {
        // ブロック面ヒット（積み重ね）は整数グリッド上なので触らない
        // Block-face hits (stacking) sit on the integer grid and stay untouched
        public static bool ShouldFollowCursorCell(bool isGroundHit)
        {
            return isGroundHit;
        }

        // Y軸へ伸びた列は各セルのYが積み上げ段数そのものなので、地形へ揃えると全セルが1セルへ潰れる
        // A run extended along Y carries the stack level in each cell's Y, so aligning them to the terrain collapses the run into one cell
        public static bool ShouldFollowRunCells(bool isGroundHit, PlacementRunAxis runAxis)
        {
            return isGroundHit && runAxis != PlacementRunAxis.Y;
        }
    }
}
