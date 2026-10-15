using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass
{
    // ベルト経路の各セルで地形が要求する下限Yを答える（ADR 0081）
    // Answers the terrain lower-bound Y for each belt-path cell (ADR 0081)
    public interface IConveyorTerrainFloor
    {
        // 地表が取れなければfalse。そのセルには下限を付けない
        // Returns false when no ground is found; that cell gets no lower bound
        bool TryGetFloorY(Vector3Int cell, out int floorY);
    }
}
