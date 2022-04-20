using MainGame.Model.Network.Event;
using MainGame.UnityView;
using MainGame.UnityView.UI.Inventory.Element;
using UnityEngine;
using VContainer;

namespace MainGame.Presenter.Inventory
{
    /// <summary>
    /// プレイヤーインベントリのGrabbedItem（インベントリでスロットをクリックしたときにマウスカーソルについてくる画像）の画像や数字の更新を行います
    /// </summary>
    public class PlayerInventoryGrabbedItemImageSet : MonoBehaviour
    {
        private InventoryItemSlot _grabbedItem;
        private int _grabbedItemIndex = 0;
        private bool _isCraftingInventory = false;
        
        private MainInventoryDataCache _mainInventoryDataCache;
        private CraftingInventoryDataCache _craftingInventoryDataCache;

        private ItemImages _itemImages;
        
        
        [Inject]
        public void Construct(MainInventoryDataCache mainInventoryDataCache,CraftingInventoryDataCache craftingInventoryDataCache,
            MainInventoryUpdateEvent mainInventoryUpdateEvent,CraftingInventoryUpdateEvent craftingInventoryUpdateEvent,ItemImages itemImages)
        {
            _mainInventoryDataCache = mainInventoryDataCache;
            _craftingInventoryDataCache = craftingInventoryDataCache;
            _grabbedItem = GetComponent<InventoryItemSlot>();
            
            _itemImages = itemImages;
            
            //grabbedItemの更新を行うためにイベントを登録
            mainInventoryUpdateEvent.OnMainInventoryUpdateEvent += MainInventoryUpdate;
            mainInventoryUpdateEvent.OnMainInventorySlotUpdateEvent += MainInventorySlotUpdate;
            craftingInventoryUpdateEvent.OnCraftingInventoryUpdate += CraftingInventoryUpdate;
            craftingInventoryUpdateEvent.OnCraftingInventorySlotUpdate += CraftingInventorySlotUpdate;
        }
        
        //メインインベントリの更新イベント
        private void MainInventoryUpdate(MainInventoryUpdateProperties properties) { }
        private void MainInventorySlotUpdate(MainInventorySlotUpdateProperties properties)
        {
            //持っているアイテムのスロットがクラフトインベントリでなく、同じであれば更新
            if (!_isCraftingInventory && properties.SlotId == _grabbedItemIndex)
            {
                MainThreadExecutionQueue.Instance.Insert(() => SetMainItem(properties.SlotId));
            }
        }
        
        //クラフトインベントリの更新イベント
        private void CraftingInventoryUpdate(CraftingInventoryUpdateProperties properties) { }
        private void CraftingInventorySlotUpdate(CraftingInventorySlotUpdateProperties properties)
        {
            //持っているアイテムのスロットがクラフトインベントリで、同じであれば更新
            if (_isCraftingInventory && properties.SlotId == _grabbedItemIndex)
            {
                MainThreadExecutionQueue.Instance.Insert(() => SetCraftItem(properties.SlotId));
            }
            
        }
        
        public void SetGrabbedMainItemSlot(int index)
        {
            _grabbedItemIndex = index;
            _isCraftingInventory = false;
            SetMainItem(index);
        }
        private void SetMainItem(int slot)
        {
            var fromItem = _mainInventoryDataCache.GetItemStack(slot);
            _grabbedItem.SetItem(_itemImages.GetItemView(fromItem.ID),fromItem.Count);
        }

        
        public void SetGrabbedCraftItemSlot(int index)
        {
            _grabbedItemIndex = index;
            _isCraftingInventory = true;
            SetCraftItem(index);
        }

        private void SetCraftItem(int slot)
        {
            var fromItem = _craftingInventoryDataCache.GetItemStack(slot);
            _grabbedItem.SetItem(_itemImages.GetItemView(fromItem.ID),fromItem.Count);
        }
    }
}