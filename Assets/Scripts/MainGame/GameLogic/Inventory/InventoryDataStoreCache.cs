using System.Collections.Generic;
using MainGame.Constant;
using MainGame.GameLogic.Event;
using MainGame.Network.Interface;
using MainGame.Network.Interface.Receive;
using Maingame.Types;
using MainGame.UnityView.Interface;
using VContainer.Unity;

namespace MainGame.GameLogic.Inventory
{
    //IInitializableがないとDIコンテナ作成時にインスタンスが生成されないので実装しておく
    public class InventoryDataStoreCache : IInitializable
    {
        private List<ItemStack> _items = new List<ItemStack>();
        private InventoryUpdateEvent _inventoryUpdateEvent;
        
        public InventoryDataStoreCache(IPlayerInventoryUpdateEvent playerInventoryUpdateEvent,
            IInventoryUpdateEvent inventoryUpdateEvent)
        {
            _inventoryUpdateEvent = inventoryUpdateEvent as InventoryUpdateEvent;
            playerInventoryUpdateEvent.Subscribe(UpdateInventory,UpdateSlotInventory);
        }

        public void UpdateInventory(OnPlayerInventoryUpdateProperties properties)
        {
            _items = properties.ItemStacks;
            //イベントの発火
            for (int i = 0; i < _items.Count; i++)
            {
                _inventoryUpdateEvent.OnOnInventoryUpdate(i,_items[i].ID,_items[i].Count);
            }
        }

        public void UpdateSlotInventory(OnPlayerInventorySlotUpdateProperties properties)
        {
            var s = properties.SlotId;
            _items[s] = properties.ItemStack;
            //イベントの発火
            _inventoryUpdateEvent.OnOnInventoryUpdate(s,_items[s].ID,_items[s].Count);
        }
        
        public ItemStack GetItem(int slot)
        {
            return _items[slot];
        }

        public void Initialize() { }
    }
}