using System;
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
    ///     鉱脈1インスタンスの占有範囲。GUIDは種別なので同じ値のインスタンスが多数ある
    ///     One vein instance's occupied range; the guid is a type, so many instances share the same value
    /// </summary>
    public class MapVeinAabb
    {
        public readonly Guid VeinTypeGuid;
        public readonly Vector3Int MinCell;
        public readonly Vector3Int MaxCell;
        public readonly MapVeinKind Kind;
        public readonly Bounds Bounds;

        // アイテム鉱脈の産出アイテム。流体鉱脈はnull
        // The item an item vein yields; null for fluid veins
        public readonly ItemId? VeinItemId;

        // 流体鉱脈の流体。アイテム鉱脈はnull
        // The fluid a fluid vein holds; null for item veins
        public readonly FluidId? VeinFluidId;

        // 種別と産出物の組は生成口で確定させ、Kind・VeinItemId・VeinFluidId の食い違った組を構文的に作れなくする
        // The factories fix kind and yield together, so no caller can even spell an inconsistent Kind / VeinItemId / VeinFluidId trio
        public static MapVeinAabb OfItem(Guid veinTypeGuid, Vector3Int minCell, Vector3Int maxCell, ItemId itemId)
        {
            return new MapVeinAabb(veinTypeGuid, minCell, maxCell, MapVeinKind.Item, itemId, null);
        }

        public static MapVeinAabb OfFluid(Guid veinTypeGuid, Vector3Int minCell, Vector3Int maxCell, FluidId fluidId)
        {
            return new MapVeinAabb(veinTypeGuid, minCell, maxCell, MapVeinKind.Fluid, null, fluidId);
        }

        private MapVeinAabb(Guid veinTypeGuid, Vector3Int minCell, Vector3Int maxCell, MapVeinKind kind, ItemId? veinItemId, FluidId? veinFluidId)
        {
            VeinTypeGuid = veinTypeGuid;
            MinCell = minCell;
            MaxCell = maxCell;
            Kind = kind;
            VeinItemId = veinItemId;
            VeinFluidId = veinFluidId;

            // min/maxは内包セル座標なのでmax側に1セル分足してワールドAABBにする
            // min/max are inclusive cell coords, so add one cell on the max side to build the world AABB
            Bounds = new Bounds();
            Bounds.SetMinMax(minCell, maxCell + Vector3Int.one);
        }
    }
}
