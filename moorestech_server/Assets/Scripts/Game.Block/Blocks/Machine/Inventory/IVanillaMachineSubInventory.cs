using System.Collections.Generic;
using Core.Item.Interface;

namespace Game.Block.Blocks.Machine.Inventory
{
    /// <summary>
    ///     統合インベントリが各スロットレンジへ委譲するためのインターフェース
    ///     Interface the unified inventory uses to delegate to each slot range
    /// </summary>
    public interface IVanillaMachineSubInventory
    {
        IReadOnlyList<IItemStack> Items { get; }
        void SetItem(int slot, IItemStack itemStack);

        // プレイヤー操作でこのローカルスロットへ置けるか（レシピ束縛の判定）。拒否時は理由を返す
        // Whether a player may place the stack into this local slot (recipe-binding rule); a rejection returns its reason
        MachineSlotPlacementCheck CheckPlacement(int localSlot, IItemStack itemStack);
    }
}
