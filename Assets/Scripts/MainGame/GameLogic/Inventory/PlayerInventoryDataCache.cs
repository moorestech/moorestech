using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;
using VContainer.Unity;

namespace MainGame.GameLogic.Inventory
{
    //IInitializableがないとDIコンテナ作成時にインスタンスが生成されないので実装しておく
    public class PlayerInventoryDataCache : IInitializable
    {
        private List<ItemStack> _items = new List<ItemStack>();
        
        public PlayerInventoryDataCache(PlayerInventoryUpdateEvent playerInventoryUpdateEvent)
        {
            playerInventoryUpdateEvent.Subscribe(UpdateInventory,UpdateSlotInventory);
        }

        public void UpdateInventory(OnPlayerInventoryUpdateProperties properties)
        {
            _items = properties.ItemStacks;
            //イベントの発火
            for (int i = 0; i < _items.Count; i++)
            {
                //TODO viewのUIにインベントリが更新されたことを通知する
                //_playerInventoryViewUpdateEvent.OnOnInventoryUpdate(i,_items[i].ID,_items[i].Count);
            }
        }

        public void UpdateSlotInventory(OnPlayerInventorySlotUpdateProperties properties)
        {
            var s = properties.SlotId;
            _items[s] = properties.ItemStack;
            //イベントの発火
            
            //TODO viewのUIにインベントリが更新されたことを通知する
            //_playerInventoryViewUpdateEvent.OnOnInventoryUpdate(s,_items[s].ID,_items[s].Count);
        }
        
        public ItemStack GetItemStack(int slot)
        {
            return _items[slot];
        }

        public void Initialize() { }
    }
}