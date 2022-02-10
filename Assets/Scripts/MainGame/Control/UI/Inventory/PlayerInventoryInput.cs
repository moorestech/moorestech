using Core.Item.Util;
using MainGame.GameLogic.Inventory;
using MainGame.Network.Event;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MainGame.Control.UI.Inventory
{
    public class PlayerInventoryInput : MonoBehaviour,IPostStartable
    {
        [SerializeField] private InventoryItemSlot equippedItem;
        
        private int _equippedItemIndex = -1;
        private MoorestechInputSettings _inputSettings;
        
        private PlayerInventoryItemView _playerInventoryItemView;
        private InventoryItemMoveService _inventoryItemMoveService;
        private PlayerInventoryDataCache _playerInventoryDataCache;

        [Inject]
        public void Construct(
            PlayerInventoryItemView playerInventoryItemView,
            InventoryItemMoveService inventoryItemMoveService,
            PlayerInventoryDataCache playerInventoryDataCache)
        {
            _playerInventoryDataCache = playerInventoryDataCache;
            _playerInventoryItemView = playerInventoryItemView;
            _inventoryItemMoveService = inventoryItemMoveService;
            
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
            if (_equippedItemIndex == -1 && !IsSlotEmpty(slot))
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
                _inventoryItemMoveService.MoveHalfItemStack(_equippedItemIndex,false,slot,false);
                return;
            }
            
            //アイテムを一個だけおく
            if (_inputSettings.UI.InventoryItemOnePut.inProgress)
            {
                _inventoryItemMoveService.MoveOneItemStack(_equippedItemIndex,false,slot,false);
                return;
            }
            
            //アイテムを全部おく
            _inventoryItemMoveService.MoveAllItemStack(_equippedItemIndex,false,slot,false);
            _equippedItemIndex = -1;
            equippedItem.gameObject.SetActive(false);
        }

        //TODO　equippedItemの更新を行うためにイベントを登録　これを外に出す
        private void PlayerInventoryUpdate(OnPlayerInventoryUpdateProperties properties) { }
        private void PlayerInventorySlotUpdate(OnPlayerInventorySlotUpdateProperties properties)
        {
            if (properties.SlotId != _equippedItemIndex) return;
            var fromItem = _playerInventoryItemView.GetInventoryItemSlots()[properties.SlotId];
            equippedItem.CopyItem(fromItem);
        }
        

        private bool IsSlotEmpty(int slot)
        {
            return _playerInventoryDataCache.GetItemStack(slot).ID == ItemConst.EmptyItemId;
        }

    }
}