using System;
using Core.Const;
using MainGame.UnityView.UI.Inventory.Control;
using MainGame.UnityView.UI.Inventory.Element;
using TMPro;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.Inventory.View
{
    public class PlayerInventoryItemNamePresenter : MonoBehaviour
    {
        [SerializeField] private PlayerInventorySlots playerInventorySlots;
        private ItemImages _itemImages;
        
        private PlayerInventoryViewModel _playerInventoryViewModel;
        private PlayerInventoryViewModelController _playerInventoryViewModelController;
        

        [Inject]
        public void Construct(PlayerInventoryViewModel playerInventoryViewModel,PlayerInventoryViewModelController playerInventoryViewModelController,ItemImages itemImages)
        {
            _playerInventoryViewModel = playerInventoryViewModel;
            _playerInventoryViewModelController = playerInventoryViewModelController;
            _itemImages = itemImages;
            playerInventorySlots.OnCursorEnter += OnCursorEnter;
            playerInventorySlots.OnCursorMove += OnCursorEnter;
            playerInventorySlots.OnCursorExit += _ => ItemNameBar.Instance.HideItemName(false);
            
            //アイテムが置かれたことを検知してアイテム名を表示する
            _playerInventoryViewModelController.OnItemSlotAdded += (slot,count) =>
            {
                if (_playerInventoryViewModelController.IsGrabbed)return;
                ItemNameBar.Instance.ShowItemName();
            };
            //持っているアイテムが更新された時はテキストも更新しておく
            _playerInventoryViewModelController.OnGrabbedItemUpdate += (item) =>
            {
                ItemNameBar.Instance.ShowItemName(_itemImages.GetItemView(item.ID).itemName);
            };
        }

        private void OnCursorEnter(int slot)
        {
            if (_playerInventoryViewModelController.IsGrabbed)return;
            
            var item = _playerInventoryViewModel[slot];
            
            if (item.Count == ItemConst.EmptyItemId)
            {
                ItemNameBar.Instance.HideItemName();
                return;
            }
            ItemNameBar.Instance.ShowItemName(_itemImages.GetItemView(item.Id).itemName);
        }
    }
    
}