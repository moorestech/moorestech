using Game.PlayerInventory.Interface;
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
        [SerializeField] private InventoryItemSlot equippedItem;
        private int _equippedItemIndex = 0;
        
        private BlockInventoryItemView _blockInventoryItemView;
        
        
        [Inject]
        public void Construct(BlockInventoryItemView playerInventoryItemView, PlayerInventoryUpdateEvent playerInventoryUpdateEvent,
            BlockInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            _blockInventoryItemView = playerInventoryItemView;
            equippedItem = GetComponent<InventoryItemSlot>();
            
            playerInventoryUpdateEvent.Subscribe(p=>{},PlayerInventorySlotUpdate);
            blockInventoryUpdateEvent.Subscribe(BlockInventorySlotUpdate,p => {});
        }
        

        //プレイヤーインベントリが更新したときにequippedItemの更新を行うためにイベントを登録
        private void PlayerInventorySlotUpdate(OnPlayerInventorySlotUpdateProperties properties)
        {
            if (properties.SlotId != _equippedItemIndex) return;
            SetItem(properties.SlotId);
        }
        
        //ブロックインベントリが更新したときにequippedItemの更新を行うためにイベントを登録
        private void BlockInventorySlotUpdate(BlockInventorySlotUpdateProperties properties)
        {
            var blockSlot = properties.Slot + PlayerInventoryConst.MainInventorySize;
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
            equippedItem.CopyItem(fromItem);
        }
    }
}