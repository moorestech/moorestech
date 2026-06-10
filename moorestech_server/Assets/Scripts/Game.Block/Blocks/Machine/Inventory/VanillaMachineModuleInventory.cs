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
    ///     機械のモジュールスロットを保持するインベントリ。統合スロット番号ではインプット・アウトプットの後ろの第3レンジとして扱う
    ///     Inventory holding the machine's module slots. In unified slot numbering it is the third range after input and output
    /// </summary>
    public class VanillaMachineModuleInventory
    {
        public IReadOnlyList<IItemStack> ModuleSlot => _itemDataStoreService.InventoryItems;

        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;
        private readonly BlockInstanceId _blockInstanceId;

        private readonly int _inputSlotSize;
        private readonly int _outputSlotSize;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;

        public VanillaMachineModuleInventory(
            int moduleSlotCount,
            BlockOpenableInventoryUpdateEvent blockInventoryUpdate,
            BlockInstanceId blockInstanceId,
            int inputSlotSize,
            int outputSlotSize)
        {
            _blockInventoryUpdate = blockInventoryUpdate;
            _blockInstanceId = blockInstanceId;
            _inputSlotSize = inputSlotSize;
            _outputSlotSize = outputSlotSize;
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, moduleSlotCount);
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

        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            // モジュールスロットは第3レンジなので、インプット＋アウトプット分のオフセットを加算して通知する
            // Module slots are the third range, so offset by input + output slot sizes when notifying
            _blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(
                _blockInstanceId, slot + _inputSlotSize + _outputSlotSize, itemStack));
        }
    }
}
