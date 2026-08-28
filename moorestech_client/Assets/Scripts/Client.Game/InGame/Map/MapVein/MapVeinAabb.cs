using Core.Master;
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

        // アイテム鉱脈の産出アイテム。流体鉱脈はnull
        // The item an item vein yields; null for fluid veins
        public readonly ItemId? VeinItemId;

        public MapVeinAabb(Vector3Int minCell, Vector3Int maxCell, MapVeinKind kind, ItemId? veinItemId)
        {
            MinCell = minCell;
            MaxCell = maxCell;
            Kind = kind;
            VeinItemId = veinItemId;

            // min/maxは内包セル座標なのでmax側に1セル分足してワールドAABBにする
            // min/max are inclusive cell coords, so add one cell on the max side to build the world AABB
            Bounds = new Bounds();
            Bounds.SetMinMax(minCell, maxCell + Vector3Int.one);
        }

    }
}
