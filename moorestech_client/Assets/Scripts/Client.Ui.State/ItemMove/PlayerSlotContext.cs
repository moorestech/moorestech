using System.Collections.Generic;
using Client.Ui.State.Items;
using Core.Item.Interface;

namespace Client.Ui.State.ItemMove
{
    /// <summary>
    /// プレイヤースロットの左クリックの判定材料。ブロック UI が開いている間だけブロック側が Shift 配分の宛先になる
    /// Inputs for a player-slot left click; the block side is the Shift target only while a block UI is open
    /// </summary>
    public abstract class PlayerSlotContext
    {
        public PlayerInventorySnapshot Inventory { get; }

        // スタック上限は研究で伸びるので、マスタ値ではなく現在の解放レベルから引く
        // Stack limits grow with research, so read them from the current unlock level, not the raw master
        public IItemStackLevelLookup StackLevels { get; }

        private protected PlayerSlotContext(PlayerInventorySnapshot inventory, IItemStackLevelLookup stackLevels)
        {
            Inventory = inventory;
            StackLevels = stackLevels;
        }

        public static PlayerSlotContext WithBlockClosed(PlayerInventorySnapshot inventory, IItemStackLevelLookup stackLevels)
        {
            return new BlockClosedSlotContext(inventory, stackLevels);
        }

        public static PlayerSlotContext WithBlockOpen(PlayerInventorySnapshot inventory, IReadOnlyList<SlotItem> blockItemSlots, BlockSlotRestriction restriction, IItemStackLevelLookup stackLevels)
        {
            return new BlockOpenSlotContext(inventory, blockItemSlots, restriction, stackLevels);
        }
    }

    // ブロック UI が閉じている。ブロック側の値を持たない
    // No block UI is open; carries no block-side values
    public sealed class BlockClosedSlotContext : PlayerSlotContext
    {
        internal BlockClosedSlotContext(PlayerInventorySnapshot inventory, IItemStackLevelLookup stackLevels) : base(inventory, stackLevels)
        {
        }
    }

    // ブロック UI が開いている。0 枠でも開いている扱い（Web の空配列も truthy と同じ）
    // A block UI is open, even with zero slots (matching the Web where an empty array is truthy)
    public sealed class BlockOpenSlotContext : PlayerSlotContext
    {
        public IReadOnlyList<SlotItem> BlockItemSlots { get; }
        public BlockSlotRestriction Restriction { get; }

        internal BlockOpenSlotContext(PlayerInventorySnapshot inventory, IReadOnlyList<SlotItem> blockItemSlots, BlockSlotRestriction restriction, IItemStackLevelLookup stackLevels) : base(inventory, stackLevels)
        {
            BlockItemSlots = blockItemSlots;
            Restriction = restriction;
        }
    }
}
