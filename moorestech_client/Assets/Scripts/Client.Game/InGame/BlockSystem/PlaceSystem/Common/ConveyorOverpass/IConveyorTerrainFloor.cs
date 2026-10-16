using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass
{
    // セルごとの地形下限Y（ADR 0081）
    // Terrain lower-bound Y per cell (ADR 0081)
    public interface IConveyorTerrainFloor
    {
        // 地表が取れなければfalse。そのセルには下限を付けない
        // Returns false when no ground is found; that cell gets no lower bound
        bool TryGetFloorY(Vector3Int cell, out int floorY);
    }
}
