using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer;

namespace MainGame.Control.UI.Inventory
{
    /// <summary>
    /// ブロックインベントリのEquippedItem（インベントリでスロットをクリックしたときにマウスカーソルについてくる画像）の画像や数字の更新を行います
    /// </summary>
    public class BlockInventoryEquippedItemImageSet : MonoBehaviour
    {
        private InventoryItemSlot _equippedItem;
        private int _equippedItemIndex = 0;
        
        private BlockInventoryItemView _blockInventoryItemView;
        
        
        [Inject]
        public void Construct(BlockInventoryItemView blockInventoryItemView, IMainInventoryUpdateEvent mainInventoryUpdateEvent,
            IBlockInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            _blockInventoryItemView = blockInventoryItemView;
            _equippedItem = GetComponent<InventoryItemSlot>();
            
            mainInventoryUpdateEvent.Subscribe(p=>{},MainInventorySlotUpdate);
            blockInventoryUpdateEvent.Subscribe(BlockInventorySlotUpdate,p => {});
        }
        

        //プレイヤーインベントリが更新したときにequippedItemの更新を行うためにイベントを登録
        private void MainInventorySlotUpdate(MainInventorySlotUpdateProperties properties)
        {
            if (properties.SlotId != _equippedItemIndex) return;
            SetItem(properties.SlotId);
        }
        
        //ブロックインベントリが更新したときにequippedItemの更新を行うためにイベントを登録
        private void BlockInventorySlotUpdate(BlockInventorySlotUpdateProperties properties)
        {
            var blockSlot = properties.Slot + PlayerInventoryConstant.MainInventorySize;
            if (blockSlot != _equippedItemIndex) return;
            SetItem(blockSlot);
        }


        public void SetEquippedItemIndex(int index)
        {
            _equippedItemIndex = index;
            SetItem(index);
        }

        private void SetItem(int slot)
        {
            var fromItem = _blockInventoryItemView.GetOpenedInventoryItemSlot(slot);
            _equippedItem.CopyItem(fromItem);
        }
    }
}