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
        
        private MainInventoryItemView _mainInventoryItemView;
        
        [Inject]
        public void Construct(MainInventoryItemView mainInventoryItemView, IMainInventoryUpdateEvent mainInventoryUpdateEvent)
        {
            _mainInventoryItemView = mainInventoryItemView;
            _equippedItem = GetComponent<InventoryItemSlot>();
            mainInventoryUpdateEvent.Subscribe(MainInventoryUpdate,MainInventorySlotUpdate);
        }
        

        //equippedItemの更新を行うためにイベントを登録
        private void MainInventoryUpdate(MainInventoryUpdateProperties properties) { }
        private void MainInventorySlotUpdate(MainInventorySlotUpdateProperties properties)
        {
            if (properties.SlotId != _equippedItemIndex) return;
            MainThreadExecutionQueue.Instance.Insert(() => SetItem(properties.SlotId));
            
        }
        
        public void SetEquippedItemIndex(int index)
        {
            _equippedItemIndex = index;
            SetItem(index);
        }

        private void SetItem(int slot)
        {
            var fromItem = _mainInventoryItemView.GetInventoryItemSlots()[slot];
            _equippedItem.CopyItem(fromItem);
        }
    }
}