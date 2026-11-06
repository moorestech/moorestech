using Client.Ui.State.ItemMove;
using Client.Ui.State.Items;
using Core.Master;

namespace Client.Tests.Ui.State.ItemMove
{
    // Web テストの slot()・inv()・ctx() の写し。スタック上限は 100
    // Ports of the Web tests' slot(), inv() and ctx(); the stack limit is 100
    public static class ItemMoveFixtures
    {
        public static SlotItem Slot(int itemId, int count) => new(new ItemId(itemId), count);

        // main は [1x98, 空]、持ち手は 9 番を grabCount 個（0 なら空）、装備は無し
        // main is [1x98, empty], the grab holds grabCount of item 9 (empty when 0), no equipment
        public static PlayerInventorySnapshot PlayerInventory(int grabCount)
        {
            var grab = 0 < grabCount ? Slot(9, grabCount) : Slot(0, 0);
            return new PlayerInventorySnapshot(new[] { Slot(1, 98), Slot(0, 0) }, grab, new SlotItem[0]);
        }

        public static PlayerSlotContext ClosedBlock(int grabCount)
        {
            return PlayerSlotContext.WithBlockClosed(PlayerInventory(grabCount), new FixedMaxStackLookup(100));
        }

        public static PlayerSlotContext OpenBlock(int grabCount, SlotItem[] blockSlots, BlockSlotRestriction restriction)
        {
            return PlayerSlotContext.WithBlockOpen(PlayerInventory(grabCount), blockSlots, restriction, new FixedMaxStackLookup(100));
        }
    }
}
