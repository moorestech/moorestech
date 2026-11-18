using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.Items;
using Core.Master;

namespace Client.Ui.State.BlockInventory.Logic.Machine
{
    /// <summary>
    /// ホストが配信するスロット束縛から、描くスロットと各スロットのゴーストを導く（ADR 0042 R7/R8・2026-08-30 裁定。Web machineSlotGhosts.ts の写し）
    /// Derives the slots to draw and each slot's ghost from the host-published binding (ADR 0042 R7/R8, 2026-08-30 ruling; port of Web machineSlotGhosts.ts)
    /// </summary>
    public static class MachineSlotGhosts
    {
        // 束縛が無い機械は全実スロットを描く（R11）。束縛があれば束縛スロット＋中身の残る束縛外スロットだけ（R7）
        // An unbound machine draws every real slot (R11); with a binding, only bound slots plus occupied unbound ones (R7)
        public static MachineSlotView BuildMachineSlotView(MachineBindingDetail machine, IReadOnlyList<SlotItem> itemSlots, IReadOnlyList<BlockFluidSlotState> fluidSlots)
        {
            var bands = machine.Layout.Split(itemSlots.Count);
            if (machine.SlotBindings.Count == 0 && machine.TankBindings.Count == 0)
            {
                return new MachineSlotView(ItemsWithoutGhost(bands.Input), ItemsWithoutGhost(bands.Output), FluidsWithoutGhost());
            }

            // 同じスロットへの束縛が重なったら後勝ち（Web の Map と同じ）
            // When bindings overlap on one slot the later one wins (same as the Web Map)
            var itemGhosts = new Dictionary<int, MachineItemGhost>();
            foreach (var binding in machine.SlotBindings) itemGhosts[binding.Slot] = new MachineItemGhost(binding.ItemId, binding.Count, binding.AcceptedItemIds);
            var fluidGhosts = new Dictionary<int, MachineFluidGhost>();
            foreach (var binding in machine.TankBindings) fluidGhosts[binding.Tank] = new MachineFluidGhost(binding.FluidId, binding.Amount);

            return new MachineSlotView(ItemBand(bands.Input), ItemBand(bands.Output), FluidBand());

            #region Internal

            List<BoundItemSlot> ItemsWithoutGhost(IReadOnlyList<int> band)
            {
                var slots = new List<BoundItemSlot>();
                foreach (var index in band) slots.Add(BoundItemSlot.WithoutGhost(index));
                return slots;
            }

            List<BoundFluidSlot> FluidsWithoutGhost()
            {
                var slots = new List<BoundFluidSlot>();
                for (var index = 0; index < fluidSlots.Count; index++) slots.Add(BoundFluidSlot.WithoutGhost(index));
                return slots;
            }

            // 束縛スロットを帯の順に並べ、続けて中身の残る束縛外スロット（返却しきれなかった残置分）を並べる
            // Bound slots in band order first, then occupied unbound slots (leftovers the refund could not return)
            List<BoundItemSlot> ItemBand(IReadOnlyList<int> band)
            {
                var slots = new List<BoundItemSlot>();
                foreach (var index in band)
                {
                    if (itemGhosts.TryGetValue(index, out var ghost)) slots.Add(BoundItemSlot.WithGhost(index, ghost));
                }
                foreach (var index in band)
                {
                    if (!itemGhosts.ContainsKey(index) && OwnedItemCounts.IsOwnedSlot(itemSlots[index])) slots.Add(BoundItemSlot.WithoutGhost(index));
                }
                return slots;
            }

            List<BoundFluidSlot> FluidBand()
            {
                var slots = new List<BoundFluidSlot>();
                for (var index = 0; index < fluidSlots.Count; index++)
                {
                    if (fluidGhosts.TryGetValue(index, out var ghost)) slots.Add(BoundFluidSlot.WithGhost(index, ghost));
                }
                for (var index = 0; index < fluidSlots.Count; index++)
                {
                    if (!fluidGhosts.ContainsKey(index) && fluidSlots[index].HasRealFluid) slots.Add(BoundFluidSlot.WithoutGhost(index));
                }
                return slots;
            }

            #endregion
        }

        // 入力帯で itemId を受け入れる束縛スロット。Shift 移動の宛先を束縛先へ絞るのに使う
        // Bound input-band slots accepting itemId; used to narrow Shift-move destinations to bound slots
        // 製作上限では絞らない。上限超えはサーバーの拒否と既存の拒否通知に任せる
        // Not narrowed by the craft cap; the server refusal and its existing notice handle that
        public static List<int> BoundMachineInputSlotsForItem(MachineBindingDetail machine, int totalItemSlots, ItemId itemId)
        {
            var inputBand = new HashSet<int>(machine.Layout.Split(totalItemSlots).Input);
            var slots = new List<int>();
            foreach (var binding in machine.SlotBindings)
            {
                if (!inputBand.Contains(binding.Slot) || !AcceptedItems.Contains(binding.AcceptedItemIds, itemId)) continue;
                slots.Add(binding.Slot);
            }
            return slots;
        }

        // 実物入りの束縛入力だけ所持/必要を出す。空はゴーストが語り、受け入れ外の残置品は通常の個数バッジに任せる
        // Only filled bound inputs show held/required; empties use the ghost and unaccepted leftovers keep the ordinary badge
        // 出さない理由（ゴースト無し・空・受け入れ外）は 3 つあるが、どれも呼び手は「バッジを足さない」だけなので理由を返さない（判断記録 D-15）
        // Three reasons not to show (no ghost, empty, unaccepted) all mean "add no badge" to the caller, so no reason is returned (D-15)
        public static bool TryGetInputRequirement(SlotItem slot, BoundItemSlot boundSlot, out MachineInputRequirement requirement)
        {
            if (!(boundSlot is GhostedItemSlot ghosted) || !OwnedItemCounts.IsOwnedSlot(slot) || !ghosted.Ghost.Accepts(slot.ItemId))
            {
                requirement = default;
                return false;
            }
            requirement = new MachineInputRequirement(slot.Count, ghosted.Ghost.Count, slot.Count < ghosted.Ghost.Count);
            return true;
        }
    }
}
