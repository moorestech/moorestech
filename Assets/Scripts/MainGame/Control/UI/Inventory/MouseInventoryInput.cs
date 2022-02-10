using MainGame.Control.Game;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MainGame.Control.UI.Inventory
{
    public class MouseInventoryInput : MonoBehaviour,IPostStartable
    {
        [SerializeField] private InventoryItemSlot equippedItem;
        
        private int _equippedItemIndex = -1;
        private PlayerInventoryItemView _playerInventoryItemView;
        
        private MoorestechInputSettings _inputSettings;

        [Inject]
        public void Construct(
            PlayerInventoryItemView playerInventoryItemView)
        {
            _playerInventoryItemView = playerInventoryItemView;
            
            equippedItem.gameObject.SetActive(false);
            _inputSettings = new();
            _inputSettings.Enable();
        }
        
        //イベントをボタンに登録する
        public void PostStart()
        {
            foreach (var slot in _playerInventoryItemView.GetInventoryItemSlots())
            {
                slot.SubscribeOnItemSlotClick(OnSlotClick);
            }
        }
        //ボタンがクリックされた時に呼び出される
        private void OnSlotClick(int slot)
        {
            if (_equippedItemIndex == -1)
            {
                var fromItem = _playerInventoryItemView.GetInventoryItemSlots()[slot];
                equippedItem.CopyItem(fromItem);
                
                _equippedItemIndex = slot;
                equippedItem.gameObject.SetActive(true);
                return;
            }

            //アイテムを半分だけおく
            if (_inputSettings.UI.InventoryItemHalve.inProgress)
            {
                //TODO _playerInventoryItemMove.MoveHalfItemStack(_equippedItemIndex,slot);
                return;
            }
            
            //アイテムを一個だけおく
            if (_inputSettings.UI.InventoryItemOnePut.inProgress)
            {
                //TODO _playerInventoryItemMove.MoveOneItemStack(_equippedItemIndex,slot);
                return;
            }
            
            //アイテムを全部おく
            //TODO _playerInventoryItemMove.MoveAllItemStack(_equippedItemIndex,slot);
            _equippedItemIndex = -1;
            equippedItem.gameObject.SetActive(false);
            
            
        }

        //equippedItemの更新を行う
        private void InventoryUpdate(int slot, int itemId, int count)
        {
            if (slot != _equippedItemIndex) return;
            var fromItem = _playerInventoryItemView.GetInventoryItemSlots()[slot];
            equippedItem.CopyItem(fromItem);
        }

    }
}