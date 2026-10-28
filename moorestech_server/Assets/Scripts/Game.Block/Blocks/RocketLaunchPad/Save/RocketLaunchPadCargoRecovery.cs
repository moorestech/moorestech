using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using UnityEngine;

namespace Game.Block.Blocks.RocketLaunchPad.Save
{
    // 表から外れた実在品は取り出し専用欄に保持する
    // Preserve physical items removed from the table in take-out slots
    internal static class RocketLaunchPadCargoRecovery
    {
        internal static int SlotCount(Dictionary<string, object> states, RocketLaunchPadSlotLayout layout)
        {
            if (states == null) return layout.TotalSlotCount;
            var save = BlockComponentStateReader.Read<RocketLaunchPadSaveJsonObject>(states, typeof(RocketLaunchPadProcessorComponent).FullName);
            var extraInputs = save.InputItems?.Count(item => item != null && 0 < item.Count && RocketLaunchPadSaveConverter.CanCreate(item) && !layout.TryGetInputSlot(item.ToItemStack().Id, out _)) ?? 0;
            var extraOutputs = save.OutputItems?.Skip(layout.OutputSlotCount).Count(item => item != null && 0 < item.Count) ?? 0;
            var hasValidPhase = save.Phase.HasValue && RocketLaunchPadSaveConverter.HoldsCargo((RocketLaunchPadPhase)save.Phase.Value);
            var cargo = hasValidPhase && RocketLaunchPadRetainedItemValidator.IsRetainableCargo(save, layout) ? 0 : Cargo(save).Count(item => item != null && 0 < item.Count && RocketLaunchPadSaveConverter.CanCreate(item));
            return layout.TotalSlotCount + extraInputs + extraOutputs + cargo;
        }

        internal static void ReturnCargo(RocketLaunchPadSaveJsonObject save, RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots)
        {
            foreach (var item in Cargo(save))
            {
                if (item == null || item.Count == 0) continue; // silent-ok: 空スタックは回収不要 / 非空の不正保存値は下で警告する
                if (RocketLaunchPadSaveConverter.CanCreate(item)) ReturnItem(item, layout, slots);
                else Debug.LogWarning($"発射台の積荷を復元できません item:{item.ItemGuidStr} count:{item.Count}");
            }
        }

        internal static void ReturnItem(ItemStackSaveJsonObject item, RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots)
        {
            for (var slot = layout.OutputSlotStart; slot < slots.GetSlotSize(); slot++)
            {
                if (slots.GetItem(slot).Count != 0) continue;
                slots.SetItemWithoutEvent(slot, item.ToItemStack());
                return;
            }
            throw new InvalidOperationException("発射台の実在品を回収するスロットが不足しています");
        }

        private static IEnumerable<ItemStackSaveJsonObject> Cargo(RocketLaunchPadSaveJsonObject save)
        {
            yield return save.CargoPayload;
            if (save.CargoMaterials != null)
                foreach (var item in save.CargoMaterials) yield return item;
        }
    }
}
