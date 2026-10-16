using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Ground
{
    // 端点と同じ地形最高点を含むセルにQ/E補正を加える（ADR 0047 / 0081）
    // Adds the Q/E offset to the cell containing the terrain maximum, as for endpoints (ADR 0047 / 0081)
    public class GroundConveyorTerrainFloor : IConveyorTerrainFloor
    {
        private readonly PlacementHeightOffset _placementHeightOffset;

        internal GroundConveyorTerrainFloor(PlacementHeightOffset placementHeightOffset)
        {
            _placementHeightOffset = placementHeightOffset;
        }

        public bool TryGetFloorY(Vector3Int cell, out int floorY)
        {
            // ベルトは1x1x1。ドラッグ中のQ/E補正を問い合わせ時点で読む
            // A belt is 1x1x1; read the Q/E offset at query time during the drag
            floorY = 0;
            // silent-ok: 地表が無いセルは下限を付けない(ADR 0081) / プレビューでそのセルが持ち上がらないことが見える
            if (!PlacementGroundCellResolver.TryResolveCellFromGround(cell, BlockDirection.North, Vector3Int.one, _placementHeightOffset.Value, out var resolved)) return false;

            floorY = resolved.y;
            return true;
        }
    }
}
