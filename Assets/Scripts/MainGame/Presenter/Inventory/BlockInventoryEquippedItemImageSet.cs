using MainGame.Basic;
using MainGame.Model.DataStore.Inventory;
using MainGame.Network.Event;
using MainGame.UnityView;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer;

namespace MainGame.Presenter.Inventory
{
    /// <summary>
    /// ブロックインベントリのEquippedItem（インベントリでスロットをクリックしたときにマウスカーソルについてくる画像）の画像や数字の更新を行います
    /// </summary>
    public class BlockInventoryEquippedItemImageSet : MonoBehaviour
    {
        private InventoryItemSlot _equippedItem;
        private int _equippedItemIndex = 0;
        
        private BlockInventoryPresenter _blockInventoryPresenter;
        private MainInventoryDataCache _mainInventoryDataCache;

        private ItemImages _itemImages;
        
        
        [Inject]
        public void Construct(BlockInventoryPresenter blockInventoryPresenter, IMainInventoryUpdateEvent mainInventoryUpdateEvent,
            IBlockInventoryUpdateEvent blockInventoryUpdateEvent, ItemImages itemImages,MainInventoryDataCache mainInventoryDataCache)
        {
            _blockInventoryPresenter = blockInventoryPresenter;
            _equippedItem = GetComponent<InventoryItemSlot>();
            _itemImages = itemImages;
            _mainInventoryDataCache = mainInventoryDataCache;
            
            mainInventoryUpdateEvent.Subscribe(p=>{},MainInventorySlotUpdate);
            blockInventoryUpdateEvent.Subscribe(BlockInventorySlotUpdate,p => {});
        }
        

        //プレイヤーインベントリが更新したときにequippedItemの更新を行うためにイベントを登録
        private void MainInventorySlotUpdate(MainInventorySlotUpdateProperties properties)
        {
            if (properties.SlotId != _equippedItemIndex) return;
            SetItem(_mainInventoryDataCache.GetItemStack(properties.SlotId));
        }
        
        //ブロックインベントリが更新したときにequippedItemの更新を行うためにイベントを登録
        private void BlockInventorySlotUpdate(BlockInventorySlotUpdateProperties properties)
        {
            var blockSlot = properties.Slot + PlayerInventoryConstant.MainInventorySize;
            if (blockSlot != _equippedItemIndex) return;
            SetItem(_blockInventoryPresenter.GetItemStack(blockSlot));
        }

        public void SetMainEquippedItemIndex(int index)
        {
            _equippedItemIndex = index;
            SetItem(_mainInventoryDataCache.GetItemStack(index));
        }

        public void SetBlockEquippedItemIndex(int index)
        {
            _equippedItemIndex = index;
            SetItem(_blockInventoryPresenter.GetItemStack(index));
        }

        private void SetItem(ItemStack itemStack)
        {
            MainThreadExecutionQueue.Instance.Insert(() =>
            {
                _equippedItem.SetItem(_itemImages.GetItemViewData(itemStack.ID),itemStack.Count);
            });
        }
    }
}