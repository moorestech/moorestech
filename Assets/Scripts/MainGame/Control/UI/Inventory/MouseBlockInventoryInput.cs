using Game.PlayerInventory.Interface;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MainGame.Control.UI.Inventory
{
    public class MouseBlockInventoryInput : MonoBehaviour,IPostStartable
    {
        [SerializeField] private InventoryItemSlot equippedItem;
        
        private int _equippedItemIndex = -1;
        private BlockInventoryItemView _blockInventoryItemView;
        private BlockInventoryItemMoveService _blockInventoryItemMoveService;
        
        private MoorestechInputSettings _inputSettings;

        [Inject]
        public void Construct(
            BlockInventoryItemView blockInventoryItemView,
            BlockInventoryItemMoveService blockInventoryItemMoveService)
        {
            _blockInventoryItemView = blockInventoryItemView;
            _blockInventoryItemMoveService = blockInventoryItemMoveService;
            
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
            if (_equippedItemIndex == -1)
            {
                var fromItem = _blockInventoryItemView.GetAllInventoryItemSlots()[slot];
                equippedItem.CopyItem(fromItem);
                
                _equippedItemIndex = slot;
                equippedItem.gameObject.SetActive(true);
                return;
            }

            int fromSlot = _equippedItemIndex;
            int toSlot = slot;
            bool toBlock = false;
            //toSlotがプレイヤーインベントリのslot数よりも多いときはブロックないのインベントリと判断する
            if (PlayerInventoryConst.MainInventorySize <= toSlot)
            {
                toBlock = true;
                toSlot -= PlayerInventoryConst.MainInventorySize;
            }
            
            //アイテムを半分だけおく
            if (_inputSettings.UI.InventoryItemHalve.inProgress)
            {
               _blockInventoryItemMoveService.MoveHalfItemStack(fromSlot,toSlot,toBlock);
                return;
            }
            
            //アイテムを一個だけおく
            if (_inputSettings.UI.InventoryItemOnePut.inProgress)
            {
                _blockInventoryItemMoveService.MoveOneItemStack(fromSlot,toSlot,toBlock);
                return;
            }
            
            //アイテムを全部おく
           _blockInventoryItemMoveService.MoveAllItemStack(fromSlot,toSlot,toBlock);
            _equippedItemIndex = -1;
            equippedItem.gameObject.SetActive(false);
            
            
        }
    }
}