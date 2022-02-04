using MainGame.UnityView.ControllerInput;
using MainGame.UnityView.Interface.PlayerInput;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace MainGame.UnityView.UI.Inventory.Control
{
    public class MouseInventoryInput : IControllerInput,IPostStartable
    {
        [SerializeField] private Image _equippedItem;
        
        private int _equippedItemIndex = -1;
        private MainInventoryItemView _mainInventoryItemView;
        private IPlayerInventoryItemMove _playerInventoryItemMove;
        private ItemImages _itemImages;
        
        private MoorestechInputSettings _inputSettings = new();

        [Inject]
        public void Construct(
            MainInventoryItemView mainInventoryItemView,
            IPlayerInventoryItemMove playerInventoryItemMove,ItemImages itemImages)
        {
            _mainInventoryItemView = mainInventoryItemView;
            _playerInventoryItemMove = playerInventoryItemMove;
            _itemImages = itemImages;
        }
        
        public void PostStart()
        {
            foreach (var slot in _mainInventoryItemView.GetInventoryItemSlots())
            {
                slot.SubscribeOnItemSlotClick(OnSlotClick);
            }
        }

        private void OnSlotClick(int slot)
        {
            if (_equippedItemIndex == -1)
            {
                _equippedItemIndex = slot;
                _equippedItem.sprite = _itemImages.GetItemImage(slot);
                _equippedItem.gameObject.SetActive(true);
                return;
            }

            //アイテムを半分だけおく
            if (_inputSettings.Playable.InventoryItemHalve.inProgress)
            {
                _playerInventoryItemMove.MoveHalfItemStack(_equippedItemIndex,slot);
                return;
            }
            
            //アイテムを一個だけおく
            if (_inputSettings.Playable.InventoryItemOnePut.inProgress)
            {
                _playerInventoryItemMove.MoveOneItemStack(_equippedItemIndex,slot);
                return;
            }
            
            //アイテムを全部おく
            _playerInventoryItemMove.MoveAllItemStack(_equippedItemIndex,slot);
            
            
        }
        
        public void OnInput()
        {
            throw new System.NotImplementedException();
        }

        public void OffInput()
        {
            throw new System.NotImplementedException();
        }

    }
}