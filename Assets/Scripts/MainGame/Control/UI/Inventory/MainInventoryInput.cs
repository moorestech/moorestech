using MainGame.Basic;
using MainGame.Control.UI.Inventory.ItemMove;
using MainGame.GameLogic.Inventory;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MainGame.Control.UI.Inventory
{
    public class MainInventoryInput : MonoBehaviour
    {
        
        private int _equippedItemIndex = -1;
        private MoorestechInputSettings _inputSettings;
        
        private MainInventoryItemView _mainInventoryItemView;
        private BlockInventoryMainInventoryItemMoveService _blockInventoryMainInventoryItemMoveService;
        private MainInventoryDataCache _mainInventoryDataCache;
        
        private PlayerInventoryEquippedItemImageSet _equippedItem;

        [Inject]
        public void Construct(
            MainInventoryItemView mainInventoryItemView, BlockInventoryMainInventoryItemMoveService blockInventoryMainInventoryItemMoveService,
            MainInventoryDataCache mainInventoryDataCache,PlayerInventoryEquippedItemImageSet equippedItem)
        {
            _mainInventoryDataCache = mainInventoryDataCache;
            _mainInventoryItemView = mainInventoryItemView;
            _blockInventoryMainInventoryItemMoveService = blockInventoryMainInventoryItemMoveService;
            _equippedItem = equippedItem;
            
            _equippedItem.gameObject.SetActive(false);
            _inputSettings = new();
            _inputSettings.Enable();
            
            
            //イベントをボタンに登録する
            foreach (var slot in _mainInventoryItemView.GetInventoryItemSlots())
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
                var slotEmpty = _mainInventoryDataCache.GetItemStack(slot).ID == ItemConstant.NullItemId;
                if (slotEmpty)return;

                _equippedItemIndex = slot;
                _equippedItem.gameObject.SetActive(true);
                _equippedItem.SetEquippedItemIndex(slot);
                return;
            }

            //アイテムを半分だけおく
            if (_inputSettings.UI.InventoryItemHalve.inProgress)
            {
                _blockInventoryMainInventoryItemMoveService.MoveHalfItemStack(_equippedItemIndex,false,slot,false);
                return;
            }
            
            //アイテムを一個だけおく
            if (_inputSettings.UI.InventoryItemOnePut.inProgress)
            {
                _blockInventoryMainInventoryItemMoveService.MoveOneItemStack(_equippedItemIndex,false,slot,false);
                return;
            }
            
            //アイテムを全部おく
            _blockInventoryMainInventoryItemMoveService.MoveAllItemStack(_equippedItemIndex,false,slot,false);
            _equippedItemIndex = -1;
            _equippedItem.gameObject.SetActive(false);
        }
    }
}