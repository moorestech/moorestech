using Client.Game.InGame.Block.Index;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// 設置モード中だけ、照準が走査する占有セルの索引を持つ（ADR 0100）
    /// Holds the occupied-cell index the aim traverses, only while in placement mode (ADR 0100)
    /// </summary>
    public static class PlacementAimTargetCells
    {
        // 設置モード外は null で、照準は物理レイの結果だけを使う
        // Null outside placement mode, where the aim uses only the physics ray result
        public static BlockOccupiedCellIndex Current { get; private set; }

        public static void Enable(BlockOccupiedCellIndex cells)
        {
            Current = cells;
        }

        public static void Disable()
        {
            Current = null;
        }
    }
}
