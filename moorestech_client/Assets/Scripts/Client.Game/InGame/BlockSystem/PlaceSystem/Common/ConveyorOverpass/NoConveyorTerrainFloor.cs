using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ConveyorOverpass
{
    // 地形なしで従来の立体交差経路を計算するための問い合わせ口
    // Terrain query used to calculate the original overpass path without terrain
    internal sealed class NoConveyorTerrainFloor : IConveyorTerrainFloor
    {
        public bool TryGetFloorY(Vector3Int cell, out int floorY)
        {
            floorY = 0;
            // silent-ok: 明示的な地形なし再計算では下限を付けない / プレビューの経路と断念案内で確認できる
            return false;
        }
    }
}
