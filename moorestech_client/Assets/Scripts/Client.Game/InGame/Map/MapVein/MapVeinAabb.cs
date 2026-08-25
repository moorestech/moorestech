using UnityEngine;

namespace Client.Game.InGame.Map.MapVein
{
    /// <summary>
    ///     鉱脈の種別。表示するボックスの色と、設置側が見たい鉱脈の絞り込みに使う
    ///     Vein kind; drives the box color and which veins the placement side wants to see
    /// </summary>
    public enum MapVeinKind
    {
        Item,
        Fluid,
    }

    /// <summary>
    ///     1つの鉱脈の占有範囲。セル座標(inclusive)とワールドAABBの両方を持つ
    ///     One vein's occupied range, held as both inclusive cell coords and a world AABB
    /// </summary>
    public class MapVeinAabb
    {
        public readonly Vector3Int MinCell;
        public readonly Vector3Int MaxCell;
        public readonly MapVeinKind Kind;
        public readonly Bounds Bounds;

        public MapVeinAabb(Vector3Int minCell, Vector3Int maxCell, MapVeinKind kind)
        {
            MinCell = minCell;
            MaxCell = maxCell;
            Kind = kind;

            // min/maxは内包セル座標なのでmax側に1セル分足してワールドAABBにする
            // min/max are inclusive cell coords, so add one cell on the max side to build the world AABB
            Bounds = new Bounds();
            Bounds.SetMinMax(minCell, maxCell + Vector3Int.one);
        }

        /// <summary>
        ///     サーバーのItemMapVeinDatastore.GetOverVeinsと同じinclusive判定
        ///     The same inclusive test as the server's ItemMapVeinDatastore.GetOverVeins
        /// </summary>
        public bool ContainsCell(Vector3Int cell)
        {
            return MinCell.x <= cell.x && cell.x <= MaxCell.x &&
                   MinCell.y <= cell.y && cell.y <= MaxCell.y &&
                   MinCell.z <= cell.z && cell.z <= MaxCell.z;
        }
    }
}
