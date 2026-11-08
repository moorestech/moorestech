using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass;
using UnityEngine;

namespace Client.Tests.PlaceSystem.ConveyorOverpass
{
    // XZごとの地形下限Yを辞書で返すテスト用の地形口。辞書に無いセルは地表なし扱い
    // Test terrain floor returning per-XZ lower-bound Y from a dictionary; a missing cell is treated as no ground
    public class FakeConveyorTerrainFloor : IConveyorTerrainFloor
    {
        private readonly Dictionary<Vector2Int, int> _floorYByXZ;

        public FakeConveyorTerrainFloor(Dictionary<Vector2Int, int> floorYByXZ)
        {
            _floorYByXZ = floorYByXZ;
        }

        public bool TryGetFloorY(Vector3Int cell, out int floorY)
        {
            return _floorYByXZ.TryGetValue(new Vector2Int(cell.x, cell.z), out floorY);
        }
    }
}
