using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.Items;

namespace Client.Ui.Screens.BlockInventory.Machine.Slots
{
    /// <summary>
    /// 機械の枠の中身を組む
    /// Builds machine slot content: ghosts from the binding, held/required on input slots only (Web slotAt)
    /// </summary>
    public static class MachineSlotContents
    {
        public static ItemSlotContent For(SlotItem slot, BoundItemSlot bound, bool showsRequirement, GameContentPresentation content)
        {
            // 所持/必要があれば個数バッジなし
            // With held/required present ItemSlot hides the count badge (C3-a ItemSlotPresentation)
            var ghost = bound is GhostedItemSlot ghosted ? new ItemSlotGhost(content.Item(ghosted.Ghost.ItemId), ghosted.Ghost.Count) : null;
            var shortage = showsRequirement && MachineSlotGhosts.TryGetInputRequirement(slot, bound, out var requirement)
                ? new ItemSlotShortage(requirement.OwnedCount, requirement.RequiredCount, requirement.Lacking, null)
                : null;
            return SlotItemContent.From(content, slot, shortage, ghost);
        }
    }
}
