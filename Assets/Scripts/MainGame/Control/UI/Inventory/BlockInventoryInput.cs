using MainGame.Basic;
using MainGame.Control.UI.Inventory.ItemMove;
using MainGame.GameLogic.Inventory;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MainGame.Control.UI.Inventory
{
    //TODO メインインベントリとブロックインベントリの移動を対応させる
    public class BlockInventoryInput : MonoBehaviour
    {
        private int _equippedItemIndex = -1;
        private BlockInventoryItemView _blockInventoryItemView;
        private BlockInventoryMainInventoryItemMoveService _blockInventoryMainInventoryItemMoveService;
        private BlockInventoryDataCache _blockInventoryDataCache;
        private MainInventoryDataCache _mainInventoryDataCache;
        private BlockInventoryEquippedItemImageSet _blockInventoryEquippedItemImageSet;
        
        private MoorestechInputSettings _inputSettings;



        [Inject]
        public void Construct(
            BlockInventoryItemView blockInventoryItemView,
            BlockInventoryMainInventoryItemMoveService blockInventoryMainInventoryItemMoveService,
            BlockInventoryDataCache blockInventoryDataCache,
            BlockInventoryEquippedItemImageSet blockInventoryEquippedItemImageSet,MainInventoryDataCache mainInventoryDataCache)
        {
            _blockInventoryItemView = blockInventoryItemView;
            _blockInventoryMainInventoryItemMoveService = blockInventoryMainInventoryItemMoveService;
            _blockInventoryDataCache = blockInventoryDataCache;
            _blockInventoryEquippedItemImageSet = blockInventoryEquippedItemImageSet;
            _mainInventoryDataCache = mainInventoryDataCache;

            _blockInventoryEquippedItemImageSet.gameObject.SetActive(false);
            _inputSettings = new();
            _inputSettings.Enable();
            
            
            //イベントをボタンに登録する
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
                //スロットがからの時はそのまま処理を終了
                if (IsSlotEmpty(slot))return;
                
                _equippedItemIndex = slot;
                //アイテムをクリックしたときに追従する画像の設定
                _blockInventoryEquippedItemImageSet.SetEquippedItemIndex(slot);
                _blockInventoryEquippedItemImageSet.gameObject.SetActive(true);
                return;
            }

            var fromSlot = _equippedItemIndex;
            var fromIsBlock = false;
            var toSlot = slot;
            var toIsBlock = false;
            //slot数がプレイヤーインベントリのslot数よりも多いときはブロックないのインベントリと判断する
            if (PlayerInventoryConstant.MainInventorySize <= fromSlot)
            {
                fromSlot -= PlayerInventoryConstant.MainInventorySize;
                fromIsBlock = true;
            }
            if (PlayerInventoryConstant.MainInventorySize <= toSlot)
            {
                toSlot -= PlayerInventoryConstant.MainInventorySize;
                toIsBlock = true;
            }
            
            //アイテムを半分だけおく
            if (_inputSettings.UI.InventoryItemHalve.inProgress)
            {
               _blockInventoryMainInventoryItemMoveService.MoveHalfItemStack(fromSlot,fromIsBlock,toSlot,toIsBlock);
                return;
            }
            
            //アイテムを一個だけおく
            if (_inputSettings.UI.InventoryItemOnePut.inProgress)
            {
                _blockInventoryMainInventoryItemMoveService.MoveOneItemStack(fromSlot,fromIsBlock,toSlot,toIsBlock);
                return;
            }
            
            //アイテムを全部おく
            _blockInventoryMainInventoryItemMoveService.MoveAllItemStack(fromSlot,fromIsBlock,toSlot,toIsBlock);
            _equippedItemIndex = -1;
            _blockInventoryEquippedItemImageSet.gameObject.SetActive(false);
        }
        
        private bool IsSlotEmpty(int slot)
        {
            if (PlayerInventoryConstant.MainInventorySize <= slot)
            {
                slot -= PlayerInventoryConstant.MainInventorySize;
                return _blockInventoryDataCache.GetItemStack(slot).ID == ItemConstant.NullItemId;
            }
            return _mainInventoryDataCache.GetItemStack(slot).ID == ItemConstant.NullItemId;
        }
    }
}