using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.Items;

namespace Client.Ui.Screens.BlockInventory.Machine.Slots
{
    /// <summary>
    /// 機械の枠の中身を組む。ゴーストは束縛から、所持/必要は素材の枠だけ（Web MachineInventoryBody の slotAt）
    /// Builds machine slot content: ghosts from the binding, held/required on input slots only (Web MachineInventoryBody slotAt)
    /// </summary>
    public static class MachineSlotContents
    {
        private static readonly ItemSlotMarks NoMarks = new(false, false, false);

        public static ItemSlotContent For(SlotItem slot, BoundItemSlot bound, bool showsRequirement, GameContentPresentation content)
        {
            // 所持/必要があれば ItemSlot が個数バッジを出さない（C3-a ItemSlotPresentation）
            // With held/required present ItemSlot hides the count badge (C3-a ItemSlotPresentation)
            var icon = slot.IsEmpty ? null : content.Item(slot.ItemId);
            int? count = slot.IsEmpty ? null : slot.Count;
            var ghost = bound is GhostedItemSlot ghosted ? new ItemSlotGhost(content.Item(ghosted.Ghost.ItemId), ghosted.Ghost.Count) : null;
            var shortage = showsRequirement && MachineSlotGhosts.TryGetInputRequirement(slot, bound, out var requirement)
                ? new ItemSlotShortage(requirement.OwnedCount, requirement.RequiredCount, requirement.Lacking, null)
                : null;
            return new ItemSlotContent(icon, count, NoMarks, shortage, ghost, null);
        }
    }
}
