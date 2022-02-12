using MainGame.Network.Event;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer;

namespace MainGame.Control.UI.Inventory
{
    public class PlayerInventoryEquippedItemImageSet : MonoBehaviour
    {
        [SerializeField] private InventoryItemSlot equippedItem;
        private int _equippedItemIndex = 0;
        
        private PlayerInventoryItemView _playerInventoryItemView;
        
        [Inject]
        public void Construct(PlayerInventoryItemView playerInventoryItemView, PlayerInventoryUpdateEvent playerInventoryUpdateEvent)
        {
            _playerInventoryItemView = playerInventoryItemView;
            equippedItem = GetComponent<InventoryItemSlot>();
            playerInventoryUpdateEvent.Subscribe(PlayerInventoryUpdate,PlayerInventorySlotUpdate);
        }
        

        //equippedItemの更新を行うためにイベントを登録
        private void PlayerInventoryUpdate(OnPlayerInventoryUpdateProperties properties) { }
        private void PlayerInventorySlotUpdate(OnPlayerInventorySlotUpdateProperties properties)
        {
            if (properties.SlotId != _equippedItemIndex) return;
            SetItem(properties.SlotId);
        }
        
        public void SetEquippedItemIndex(int index)
        {
            _equippedItemIndex = index;
            SetItem(index);
        }

        private void SetItem(int slot)
        {
            var fromItem = _playerInventoryItemView.GetInventoryItemSlots()[slot];
            equippedItem.CopyItem(fromItem);
        }
    }
}