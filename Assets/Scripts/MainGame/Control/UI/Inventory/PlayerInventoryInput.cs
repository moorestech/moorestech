using MainGame.Basic;
using MainGame.GameLogic.Inventory;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MainGame.Control.UI.Inventory
{
    public class PlayerInventoryInput : MonoBehaviour
    {
        
        private int _equippedItemIndex = -1;
        private MoorestechInputSettings _inputSettings;
        
        private PlayerInventoryItemView _playerInventoryItemView;
        private BlockInventoryPlayerInventoryItemMoveService _blockInventoryPlayerInventoryItemMoveService;
        private PlayerInventoryDataCache _playerInventoryDataCache;
        
        private PlayerInventoryEquippedItemImageSet _equippedItem;

        [Inject]
        public void Construct(
            PlayerInventoryItemView playerInventoryItemView, BlockInventoryPlayerInventoryItemMoveService blockInventoryPlayerInventoryItemMoveService,
            PlayerInventoryDataCache playerInventoryDataCache,PlayerInventoryEquippedItemImageSet equippedItem)
        {
            _playerInventoryDataCache = playerInventoryDataCache;
            _playerInventoryItemView = playerInventoryItemView;
            _blockInventoryPlayerInventoryItemMoveService = blockInventoryPlayerInventoryItemMoveService;
            _equippedItem = equippedItem;
            
            _equippedItem.gameObject.SetActive(false);
            _inputSettings = new();
            _inputSettings.Enable();
            
            
            //イベントをボタンに登録する
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
                //スロットがからの時はそのまま処理を終了
                var slotEmpty = _playerInventoryDataCache.GetItemStack(slot).ID == ItemConstant.NullItemId;
                if (slotEmpty)return;

                _equippedItemIndex = slot;
                _equippedItem.gameObject.SetActive(true);
                _equippedItem.SetEquippedItemIndex(slot);
                return;
            }

            //アイテムを半分だけおく
            if (_inputSettings.UI.InventoryItemHalve.inProgress)
            {
                _blockInventoryPlayerInventoryItemMoveService.MoveHalfItemStack(_equippedItemIndex,false,slot,false);
                return;
            }
            
            //アイテムを一個だけおく
            if (_inputSettings.UI.InventoryItemOnePut.inProgress)
            {
                _blockInventoryPlayerInventoryItemMoveService.MoveOneItemStack(_equippedItemIndex,false,slot,false);
                return;
            }
            
            //アイテムを全部おく
            _blockInventoryPlayerInventoryItemMoveService.MoveAllItemStack(_equippedItemIndex,false,slot,false);
            _equippedItemIndex = -1;
            _equippedItem.gameObject.SetActive(false);
        }
    }
}