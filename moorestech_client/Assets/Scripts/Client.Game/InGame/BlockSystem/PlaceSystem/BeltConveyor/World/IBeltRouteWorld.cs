using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World
{
    /// <summary>
    ///     探索が読む世界。地形・占有・坂・辺をセル単位で答える
    ///     World the search reads: terrain, occupancy, slopes, edges per cell
    ///     面ビットは BeltRouteFaceBits.FaceBit の並び（面 0..3 + 4×高さ差 0/1）
    ///     Face bits follow BeltRouteFaceBits.FaceBit (face 0..3 + 4 x height offset 0/1)
    /// </summary>
    public interface IBeltRouteWorld
    {
        bool IsBlockedByTerrain(Vector3Int cell);
        bool IsBlockedByBlock(Vector3Int cell);
        bool IsSlopeHead(Vector3Int cell);
        int OutputFaceBits(Vector3Int cell);
        int InputFaceBits(Vector3Int cell);
    }
}
