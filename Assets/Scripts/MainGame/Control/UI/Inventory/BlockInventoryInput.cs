using Core.Item.Util;
using Game.PlayerInventory.Interface;
using MainGame.GameLogic.Inventory;
using MainGame.UnityView.UI.Inventory.View;
using UnityEditor;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MainGame.Control.UI.Inventory
{
    public class BlockInventoryInput : MonoBehaviour,IPostStartable
    {
        [SerializeField] private InventoryItemSlot equippedItem;
        
        private int _equippedItemIndex = -1;
        private BlockInventoryItemView _blockInventoryItemView;
        private InventoryItemMoveService _inventoryItemMoveService;
        private BlockInventoryDataCache _blockInventoryDataCache;
        
        private MoorestechInputSettings _inputSettings;


        [Inject]
        public void Construct(
            BlockInventoryItemView blockInventoryItemView,
            InventoryItemMoveService inventoryItemMoveService,
            BlockInventoryDataCache blockInventoryDataCache)
        {
            _blockInventoryItemView = blockInventoryItemView;
            _inventoryItemMoveService = inventoryItemMoveService;
            _blockInventoryDataCache = blockInventoryDataCache;
            
            equippedItem.gameObject.SetActive(false);
            _inputSettings = new();
            _inputSettings.Enable();
        }
        
        
        
        //イベントをボタンに登録する
        public void PostStart()
        {
            foreach (var slot in _blockInventoryItemView.GetAllInventoryItemSlots())
            {
                slot.SubscribeOnItemSlotClick(OnSlotClick);
            }
        }
        
        
        
        //ボタンがクリックされた時に呼び出される
        private void OnSlotClick(int slot)
        {
            if (_equippedItemIndex == -1 && !isSlotEmpty(slot))
            {
                var fromItem = _blockInventoryItemView.GetAllInventoryItemSlots()[slot];
                equippedItem.CopyItem(fromItem);
                
                _equippedItemIndex = slot;
                equippedItem.gameObject.SetActive(true);
                return;
            }

            var fromSlot = _equippedItemIndex;
            var fromIsBlock = false;
            var toSlot = slot;
            var toIsBlock = false;
            //slot数がプレイヤーインベントリのslot数よりも多いときはブロックないのインベントリと判断する
            if (PlayerInventoryConst.MainInventorySize <= fromSlot)
            {
                fromSlot -= PlayerInventoryConst.MainInventorySize;
                fromIsBlock = true;
            }
            if (PlayerInventoryConst.MainInventorySize <= toSlot)
            {
                toSlot -= PlayerInventoryConst.MainInventorySize;
                toIsBlock = true;
            }
            
            //アイテムを半分だけおく
            if (_inputSettings.UI.InventoryItemHalve.inProgress)
            {
               _inventoryItemMoveService.MoveHalfItemStack(fromSlot,fromIsBlock,toSlot,toIsBlock);
                return;
            }
            
            //アイテムを一個だけおく
            if (_inputSettings.UI.InventoryItemOnePut.inProgress)
            {
                _inventoryItemMoveService.MoveOneItemStack(fromSlot,fromIsBlock,toSlot,toIsBlock);
                return;
            }
            
            //アイテムを全部おく
            _inventoryItemMoveService.MoveAllItemStack(fromSlot,fromIsBlock,toSlot,toIsBlock);
            _equippedItemIndex = -1;
            equippedItem.gameObject.SetActive(false);
            
            
        }

        private bool isSlotEmpty(int slot)
        {
            return _blockInventoryDataCache.GetItemStack(slot).ID == ItemConst.EmptyItemId;
        }
    }
}