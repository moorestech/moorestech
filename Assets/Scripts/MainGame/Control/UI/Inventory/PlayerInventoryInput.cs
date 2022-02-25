using MainGame.Basic;
using MainGame.Control.UI.Inventory.ItemMove;
using MainGame.GameLogic.Inventory;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MainGame.Control.UI.Inventory
{
    public class PlayerInventoryInput : MonoBehaviour
    {
        
        private int _equippedItemSlot = -1;
        private bool _isFromCrafting = false;
        
        private MoorestechInputSettings _inputSettings;

        private PlayerInventoryMainInventoryItemMoveService _playerInventoryMainInventoryItemMoveService;
        
        private MainInventoryItemView _mainInventoryItemView;
        private MainInventoryDataCache _mainInventoryDataCache;
        
        private CraftingInventoryItemView _craftingInventoryItemView;
        private CraftingInventoryDataCache _craftingInventoryDataCache;
        
        private PlayerInventoryEquippedItemImageSet _equippedItem;

        [Inject]
        public void Construct(
            PlayerInventoryEquippedItemImageSet equippedItem,PlayerInventoryMainInventoryItemMoveService playerInventoryMainInventoryItemMoveService,
            MainInventoryItemView mainInventoryItemView, MainInventoryDataCache mainInventoryDataCache,
            CraftingInventoryItemView craftingInventoryItemView, CraftingInventoryDataCache craftingInventoryDataCache
            )
        {
            _mainInventoryDataCache = mainInventoryDataCache;
            _mainInventoryItemView = mainInventoryItemView;
            _craftingInventoryDataCache = craftingInventoryDataCache;
            _craftingInventoryItemView = craftingInventoryItemView;
            _playerInventoryMainInventoryItemMoveService = playerInventoryMainInventoryItemMoveService;
            _equippedItem = equippedItem;
            
            _equippedItem.gameObject.SetActive(false);
            _inputSettings = new();
            _inputSettings.Enable();
            
            
            //イベントをそれぞれのインベントリのボタンに登録する
            foreach (var slot in _mainInventoryItemView.GetInventoryItemSlots())
            {
                slot.SubscribeOnItemSlotClick(OnMainSlotClick);
            }
            foreach (var slot in _craftingInventoryItemView.GetInventoryItemSlots())
            {
                slot.SubscribeOnItemSlotClick(OnCraftSlotClick);
            }
        }


        //クラフトインベントリのボタンがクリックされた時に呼び出される
        private void OnCraftSlotClick(int slot)
        {
            if (_equippedItemSlot == -1)
            {
                //スロットがからの時はそのまま処理を終了
                var slotEmpty = _craftingInventoryDataCache.GetItemStack(slot).ID == ItemConstant.NullItemId;
                if (slotEmpty)return;

                _isFromCrafting = true;
                _equippedItemSlot = slot;
                _equippedItem.gameObject.SetActive(true);
                _equippedItem.SetEquippedMainItemSlot(slot);
                return;
            }
            _equippedItemSlot = -1;
            _equippedItem.gameObject.SetActive(false);
            
            MoveItem(_equippedItemSlot,_isFromCrafting,slot,true);
        }
        
        //メインインベントリのボタンがクリックされた時に呼び出される
        private void OnMainSlotClick(int slot)
        {
            if (_equippedItemSlot == -1)
            {
                //スロットがからの時はそのまま処理を終了
                var slotEmpty = _mainInventoryDataCache.GetItemStack(slot).ID == ItemConstant.NullItemId;
                if (slotEmpty)return;

                _isFromCrafting = false;
                _equippedItemSlot = slot;
                _equippedItem.gameObject.SetActive(true);
                _equippedItem.SetEquippedMainItemSlot(slot);
                return;
            }

            MoveItem(_equippedItemSlot,_isFromCrafting,slot,false);
            _equippedItemSlot = -1;
            _equippedItem.gameObject.SetActive(false);
        }

        /// <summary>
        /// 実際にアイテムを移動させるサービスにアイテム移動を指示する
        /// </summary>
        private void MoveItem(int fromSlot,bool fromIsCrafting,int toSlot,bool toIsCrafting)
        {
            //アイテムを半分だけおく
            if (_inputSettings.UI.InventoryItemHalve.inProgress)
            {
                _playerInventoryMainInventoryItemMoveService.MoveHalfItemStack(fromSlot, fromIsCrafting, toSlot, toIsCrafting);
                return;
            }
            
            //アイテムを一個だけおく
            if (_inputSettings.UI.InventoryItemOnePut.inProgress)
            {
                _playerInventoryMainInventoryItemMoveService.MoveOneItemStack(fromSlot, fromIsCrafting, toSlot, toIsCrafting);
                return;
            }
            
            //アイテムを全部おく
            _playerInventoryMainInventoryItemMoveService.MoveAllItemStack(fromSlot, fromIsCrafting, toSlot, toIsCrafting);
        }
    }
}