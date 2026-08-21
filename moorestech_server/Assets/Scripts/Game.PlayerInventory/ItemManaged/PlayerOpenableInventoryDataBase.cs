using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface.Event;

namespace Game.PlayerInventory.ItemManaged
{
    /// <summary>
    ///     プレイヤー所持インベントリの共通実体。差分は更新イベントの宛先だけなので具体側はそこだけ実装する
    ///     The shared body of a player-held inventory; the only difference is the update event's destination, so that is all a subclass implements
    /// </summary>
    public abstract class PlayerOpenableInventoryDataBase : IOpenableInventory
    {
        public IReadOnlyList<IItemStack> InventoryItems => OpenableInventoryService.InventoryItems;

        protected readonly OpenableInventoryItemDataStoreService OpenableInventoryService;
        private readonly int _playerId;

        protected PlayerOpenableInventoryDataBase(int playerId, int slotCount)
        {
            _playerId = playerId;
            OpenableInventoryService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, slotCount);
        }

        public IItemStack GetItem(int slot)
        {
            return OpenableInventoryService.GetItem(slot);
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            OpenableInventoryService.SetItem(slot, itemStack);
        }

        public void SetItem(int slot, ItemId itemId, int count)
        {
            OpenableInventoryService.SetItem(slot, itemId, count);
        }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            return OpenableInventoryService.ReplaceItem(slot, itemStack);
        }

        public IItemStack ReplaceItem(int slot, ItemId itemId, int count)
        {
            return OpenableInventoryService.ReplaceItem(slot, itemId, count);
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            return OpenableInventoryService.InsertItem(itemStack);
        }

        public IItemStack InsertItem(ItemId itemId, int count)
        {
            return OpenableInventoryService.InsertItem(itemId, count);
        }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            return OpenableInventoryService.InsertItem(itemStacks);
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            return OpenableInventoryService.InsertionCheck(itemStacks);
        }

        public int GetSlotSize()
        {
            return OpenableInventoryService.GetSlotSize();
        }

        public ReadOnlyCollection<IItemStack> CreateCopiedItems()
        {
            return OpenableInventoryService.CreateCopiedItems();
        }

        // 更新の宛先イベントだけが具体側の責務
        // Routing the update to its event is the subclass's only responsibility
        protected abstract void InvokeInventoryUpdateEvent(PlayerInventoryUpdateEventProperties properties);

        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            InvokeInventoryUpdateEvent(new PlayerInventoryUpdateEventProperties(_playerId, slot, itemStack));
        }
    }
}
