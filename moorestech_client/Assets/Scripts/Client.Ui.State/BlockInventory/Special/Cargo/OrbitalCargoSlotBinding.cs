using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.Items;
using Core.Master;

namespace Client.Ui.State.BlockInventory.Special.Cargo
{
    /// <summary>
    /// 積荷の選択から、透かしつきの枠の並びと Shift の束縛を組む（未選択は何も受けない。ADR 0106 の 2）
    /// Builds the ghosted slot order and the Shift bindings from the cargo selection (unselected accepts nothing; ADR 0106 §2)
    /// </summary>
    public static class OrbitalCargoSlotBinding
    {
        // 資材の枠は全部、入れる品と必要な数の透かしを持つ（本文に出るのは選んだ後だけ）
        // Every material slot carries a ghost of its item and required count (the body shows them only after selecting)
        public static IReadOnlyList<BoundItemSlot> Materials(IReadOnlyList<RequiredItemSlot> materialSlots)
        {
            var slots = new List<BoundItemSlot>(materialSlots.Count);
            foreach (var required in materialSlots) slots.Add(Ghosted(required));
            return slots;
        }

        // 選んだ積荷の枠を透かしつきで先に、続けて中身の残る他の積荷の枠（選び直しで返しきれなかった前の種類）を並べる
        // The selected payload slot with its ghost first, then other payload slots still holding items (leftovers a reselection could not refund)
        public static IReadOnlyList<BoundItemSlot> Payloads(IReadOnlyList<RequiredItemSlot> payloadSlots, OrbitalCargoDetail cargo, IReadOnlyList<SlotItem> itemSlots)
        {
            // 「選んだ種類か」の判定は OrbitalCargoDetail の 1 か所に寄せる
            // The "is it the selected kind" check lives only in OrbitalCargoDetail
            var slots = new List<BoundItemSlot>();
            foreach (var required in payloadSlots)
            {
                if (cargo.IsSelectedChoice(required.ItemId)) slots.Add(Ghosted(required));
            }
            foreach (var required in payloadSlots)
            {
                if (!cargo.IsSelectedChoice(required.ItemId) && OwnedItemCounts.IsOwnedSlot(itemSlots[required.Slot])) slots.Add(BoundItemSlot.WithoutGhost(required.Slot));
            }
            return slots;
        }

        // 拠点の入口は選んでいれば 1 便ぶんの透かし、未選択なら透かし無し（入口は 1 枠で、残りもこの枠に出る）
        // The base inlet carries one flight's ghost when selected and none otherwise (one inlet slot; leftovers show here too)
        public static IReadOnlyList<BoundItemSlot> Inlet(int inletSlot, OrbitalCargoDetail cargo)
        {
            if (!cargo.TryGetSelected(out var selected)) return new[] { BoundItemSlot.WithoutGhost(inletSlot) };
            return new[] { BoundItemSlot.WithGhost(inletSlot, new MachineItemGhost(selected.PayloadItemId, selected.CountPerLaunch, new[] { selected.PayloadItemId })) };
        }

        // 発射台の Shift: 未選択は空（拒否が確定）、選べば資材の枠と選んだ積荷の枠だけ
        // Launch pad Shift: empty when unselected (a confirmed refusal); after selecting, only material slots and the selected payload slot
        public static IReadOnlyList<InletSlotBinding> PadShiftBindings(IReadOnlyList<RequiredItemSlot> materialSlots, IReadOnlyList<RequiredItemSlot> payloadSlots, OrbitalCargoDetail cargo)
        {
            var bindings = new List<InletSlotBinding>();
            if (!cargo.IsSelected) return bindings;
            foreach (var required in materialSlots) bindings.Add(new InletSlotBinding(required.Slot, new[] { required.ItemId }));
            foreach (var required in payloadSlots)
            {
                if (cargo.IsSelectedChoice(required.ItemId)) bindings.Add(new InletSlotBinding(required.Slot, new[] { required.ItemId }));
            }
            return bindings;
        }

        // 拠点の Shift: 未選択は空、選べば入口に選んだ種類だけ
        // Elevator base Shift: empty when unselected; after selecting, only the selected kind on the inlet
        public static IReadOnlyList<InletSlotBinding> ElevatorShiftBindings(int inletSlot, ItemId? selected)
        {
            if (!selected.HasValue) return Array.Empty<InletSlotBinding>();
            return new[] { new InletSlotBinding(inletSlot, new[] { selected.Value }) };
        }

        private static BoundItemSlot Ghosted(RequiredItemSlot required)
        {
            return BoundItemSlot.WithGhost(required.Slot, new MachineItemGhost(required.ItemId, required.Count, new[] { required.ItemId }));
        }
    }
}
