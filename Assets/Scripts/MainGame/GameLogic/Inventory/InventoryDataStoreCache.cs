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
        private PlayerInventoryViewUpdateEvent _playerInventoryViewUpdateEvent;
        
        public InventoryDataStoreCache(IPlayerInventoryUpdateEvent playerInventoryUpdateEvent,
            IPlayerInventoryViewUpdateEvent playerInventoryViewUpdateEvent)
        {
            _playerInventoryViewUpdateEvent = playerInventoryViewUpdateEvent as PlayerInventoryViewUpdateEvent;
            playerInventoryUpdateEvent.Subscribe(UpdateInventory,UpdateSlotInventory);
        }

        public void UpdateInventory(OnPlayerInventoryUpdateProperties properties)
        {
            _items = properties.ItemStacks;
            //イベントの発火
            for (int i = 0; i < _items.Count; i++)
            {
                _playerInventoryViewUpdateEvent.OnOnInventoryUpdate(i,_items[i].ID,_items[i].Count);
            }
        }

        public void UpdateSlotInventory(OnPlayerInventorySlotUpdateProperties properties)
        {
            var s = properties.SlotId;
            _items[s] = properties.ItemStack;
            //イベントの発火
            _playerInventoryViewUpdateEvent.OnOnInventoryUpdate(s,_items[s].ID,_items[s].Count);
        }
        
        public ItemStack GetItem(int slot)
        {
            return _items[slot];
        }

        public void Initialize() { }
    }
}