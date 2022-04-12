using MainGame.GameLogic.Inventory;
using MainGame.Model.DataStore.Inventory;
using MainGame.Network.Event;
using MainGame.UnityView;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer;

namespace MainGame.Presenter.Inventory
{
    /// <summary>
    /// プレイヤーインベントリのEquippedItem（インベントリでスロットをクリックしたときにマウスカーソルについてくる画像）の画像や数字の更新を行います
    /// </summary>
    public class PlayerInventoryEquippedItemImageSet : MonoBehaviour
    {
        private InventoryItemSlot _equippedItem;
        private int _equippedItemIndex = 0;
        private bool _isCraftingInventory = false;
        
        private MainInventoryDataCache _mainInventoryDataCache;
        private CraftingInventoryDataCache _craftingInventoryDataCache;

        private ItemImages _itemImages;
        
        
        [Inject]
        public void Construct(MainInventoryDataCache mainInventoryDataCache,CraftingInventoryDataCache craftingInventoryDataCache,
            IMainInventoryUpdateEvent mainInventoryUpdateEvent,ICraftingInventoryUpdateEvent craftingInventoryUpdateEvent,ItemImages itemImages)
        {
            _mainInventoryDataCache = mainInventoryDataCache;
            _craftingInventoryDataCache = craftingInventoryDataCache;
            _equippedItem = GetComponent<InventoryItemSlot>();
            
            _itemImages = itemImages;
            
            //equippedItemの更新を行うためにイベントを登録
            mainInventoryUpdateEvent.Subscribe(MainInventoryUpdate,MainInventorySlotUpdate);
            craftingInventoryUpdateEvent.Subscribe(CraftingInventoryUpdate,CraftingInventorySlotUpdate);
        }
        
        //メインインベントリの更新イベント
        private void MainInventoryUpdate(MainInventoryUpdateProperties properties) { }
        private void MainInventorySlotUpdate(MainInventorySlotUpdateProperties properties)
        {
            //持っているアイテムのスロットがクラフトインベントリでなく、同じであれば更新
            if (!_isCraftingInventory && properties.SlotId == _equippedItemIndex)
            {
                MainThreadExecutionQueue.Instance.Insert(() => SetMainItem(properties.SlotId));
            }
        }
        
        //クラフトインベントリの更新イベント
        private void CraftingInventoryUpdate(CraftingInventoryUpdateProperties properties) { }
        private void CraftingInventorySlotUpdate(CraftingInventorySlotUpdateProperties properties)
        {
            //持っているアイテムのスロットがクラフトインベントリで、同じであれば更新
            if (_isCraftingInventory && properties.SlotId == _equippedItemIndex)
            {
                MainThreadExecutionQueue.Instance.Insert(() => SetCraftItem(properties.SlotId));
            }
            
        }
        
        public void SetEquippedMainItemSlot(int index)
        {
            _equippedItemIndex = index;
            _isCraftingInventory = false;
            SetMainItem(index);
        }
        private void SetMainItem(int slot)
        {
            var fromItem = _mainInventoryDataCache.GetItemStack(slot);
            _equippedItem.SetItem(_itemImages.GetItemViewData(fromItem.ID),fromItem.Count);
        }

        
        public void SetEquippedCraftItemSlot(int index)
        {
            _equippedItemIndex = index;
            _isCraftingInventory = true;
            SetCraftItem(index);
        }

        private void SetCraftItem(int slot)
        {
            var fromItem = _craftingInventoryDataCache.GetItemStack(slot);
            _equippedItem.SetItem(_itemImages.GetItemViewData(fromItem.ID),fromItem.Count);
        }
    }
}