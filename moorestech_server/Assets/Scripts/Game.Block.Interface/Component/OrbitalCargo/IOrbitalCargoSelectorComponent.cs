using Core.Inventory;
using Core.Master;

namespace Game.Block.Interface.Component
{
    /// <summary>
    ///     発射台と宇宙エレベーター拠点が共有する積荷の選択口。未選択へ戻す操作は持たない（ADR 0106 2・5）
    ///     The cargo selection surface shared by launch pads and space elevator bases; it has no unselect operation (ADR 0106 2, 5)
    /// </summary>
    public interface IOrbitalCargoSelectorComponent : IBlockComponent
    {
        OrbitalCargoSelection Selection { get; }

        // 選び直しで外れた種類の入れ口の中身は refundInventory へ返し、入らない分はスロットに残す
        // Contents of inlets for kinds dropped by a reselection go to refundInventory; what does not fit stays in the slot
        OrbitalCargoSelectionResult SelectCargo(ItemId payloadItemId, IOpenableInventory refundInventory);
    }

    public enum OrbitalCargoSelectionResult
    {
        Success,
        NotCarriedHere,
        Locked,
    }
}
