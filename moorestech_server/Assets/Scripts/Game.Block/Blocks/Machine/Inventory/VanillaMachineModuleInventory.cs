using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Event;
using Game.Context;

namespace Game.Block.Blocks.Machine.Inventory
{
    /// <summary>
    ///     機械のモジュールスロット（統合スロットの第3レンジ）
    ///     The machine's module slots (third range in unified numbering)
    /// </summary>
    public class VanillaMachineModuleInventory : IVanillaMachineSubInventory
    {
        public IReadOnlyList<IItemStack> ModuleSlot => _itemDataStoreService.InventoryItems;
        IReadOnlyList<IItemStack> IVanillaMachineSubInventory.Items => ModuleSlot;

        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;

        public VanillaMachineModuleInventory(
            int moduleSlotCount,
            BlockOpenableInventoryUpdateEvent blockInventoryUpdate,
            BlockInstanceId blockInstanceId,
            int inputSlotSize,
            int outputSlotSize)
        {
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, moduleSlotCount);

            #region Internal

            void InvokeEvent(int slot, IItemStack itemStack)
            {
                // 第3レンジのためオフセットを加算して通知
                // Offset by the first two ranges when notifying
                blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(
                    blockInstanceId, slot + inputSlotSize + outputSlotSize, itemStack));
            }

            #endregion
        }

        public IItemStack GetItem(int slot)
        {
            return _itemDataStoreService.GetItem(slot);
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            _itemDataStoreService.SetItem(slot, itemStack);
        }

        public void SetItemWithoutEvent(int slot, IItemStack itemStack)
        {
            _itemDataStoreService.SetItemWithoutEvent(slot, itemStack);
        }
    }
}
