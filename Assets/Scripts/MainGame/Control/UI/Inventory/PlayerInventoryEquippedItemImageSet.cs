using MainGame.Network.Event;
using MainGame.UnityView;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer;

namespace MainGame.Control.UI.Inventory
{
    /// <summary>
    /// プレイヤーインベントリのEquippedItem（インベントリでスロットをクリックしたときにマウスカーソルについてくる画像）の画像や数字の更新を行います
    /// </summary>
    public class PlayerInventoryEquippedItemImageSet : MonoBehaviour
    {
        private InventoryItemSlot _equippedItem;
        private int _equippedItemIndex = 0;
        private bool _isCraftingInventory = false;
        
        private MainInventoryItemView _mainInventoryItemView;
        private CraftingInventoryItemView _craftingInventoryItemView;
        
        [Inject]
        public void Construct(MainInventoryItemView mainInventoryItemView,CraftingInventoryItemView craftingInventoryItemView,
            IMainInventoryUpdateEvent mainInventoryUpdateEvent,ICraftingInventoryUpdateEvent craftingInventoryUpdateEvent)
        {
            _mainInventoryItemView = mainInventoryItemView;
            _craftingInventoryItemView = craftingInventoryItemView;
            _equippedItem = GetComponent<InventoryItemSlot>();
            
            
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

        
        public void SetEquippedCraftItemSlot(int index)
        {
            _equippedItemIndex = index;
            _isCraftingInventory = true;
            SetCraftItem(index);
        }

        private void SetMainItem(int slot)
        {
            var fromItem = _mainInventoryItemView.GetInventoryItemSlots()[slot];
            _equippedItem.CopyItem(fromItem);
        }
        private void SetCraftItem(int slot)
        {
            var fromItem = _craftingInventoryItemView.GetInventoryItemSlots()[slot];
            _equippedItem.CopyItem(fromItem);
        }
    }
}