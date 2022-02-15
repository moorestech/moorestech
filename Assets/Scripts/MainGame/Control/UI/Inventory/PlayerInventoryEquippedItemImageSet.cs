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
        
        private PlayerInventoryItemView _playerInventoryItemView;
        
        [Inject]
        public void Construct(PlayerInventoryItemView playerInventoryItemView, PlayerInventoryUpdateEvent playerInventoryUpdateEvent)
        {
            _playerInventoryItemView = playerInventoryItemView;
            _equippedItem = GetComponent<InventoryItemSlot>();
            playerInventoryUpdateEvent.Subscribe(PlayerInventoryUpdate,PlayerInventorySlotUpdate);
        }
        

        //equippedItemの更新を行うためにイベントを登録
        private void PlayerInventoryUpdate(OnPlayerInventoryUpdateProperties properties) { }
        private void PlayerInventorySlotUpdate(OnPlayerInventorySlotUpdateProperties properties)
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
            var fromItem = _playerInventoryItemView.GetInventoryItemSlots()[slot];
            _equippedItem.CopyItem(fromItem);
        }
    }
}