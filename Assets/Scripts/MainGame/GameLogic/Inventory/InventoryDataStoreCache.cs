using System.Collections.Generic;
using MainGame.Constant;
using MainGame.Network.Interface;
using MainGame.Network.Interface.Receive;
using Maingame.Types;
using VContainer.Unity;

namespace MainGame.GameLogic.Inventory
{
    //IInitializableがないとDIコンテナ作成時にインスタンスが生成されないので実装しておく
    public class InventoryDataStoreCache : IInitializable
    {
        private List<ItemStack> _items = new List<ItemStack>();
        public InventoryDataStoreCache(IPlayerInventoryUpdateEvent playerInventoryUpdateEvent)
        {
            playerInventoryUpdateEvent.Subscribe(UpdateInventory,UpdateSlotInventory);
        }

        public void UpdateInventory(OnPlayerInventoryUpdateProperties properties)
        {
            _items = properties.ItemStacks;
        }

        public void UpdateSlotInventory(OnPlayerInventorySlotUpdateProperties properties)
        {
            _items[properties.SlotId] = properties.ItemStack;
        }

        public void Initialize() { }
    }
}