using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.Define;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Input;
using Core.Const;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.Inventory
{
    public class HotBarView : MonoBehaviour
    {
        [SerializeField] private List<HotBarItem> hotBarItems;
        [SerializeField] private ItemObjectContainer itemObjectContainer;
        [SerializeField] private PlayerGrabItemManager playerGrabItemManager;
        
        private GameObject _currentGrabItem;
        private ILocalPlayerInventory _localPlayerInventory;
        
        public IItemStack CurrentItem => _localPlayerInventory[PlayerInventoryConst.HotBarSlotToInventorySlot(SelectIndex)];
        public int SelectIndex { get; private set; }
        
        private void Start()
        {
            SelectIndex = 0;
            UpdateSelectedView(0, 0);
            for (var i = 0; i < hotBarItems.Count; i++)
            {
                var keyBordText = (i + 1).ToString();
                hotBarItems[i].SetKeyBoardText(keyBordText);
            }
        }
        
        private void Update()
        {
            UpdateHotBarItem();
            var nextSelectIndex = SelectedHotBar();
            if (nextSelectIndex != -1 && nextSelectIndex != SelectIndex)
            {
                UpdateSelectedView(SelectIndex, nextSelectIndex);
                UpdateHoldItem(nextSelectIndex); //アイテムの再生成があるので変化を検知して変更する
                
                SelectIndex = nextSelectIndex;
            }
            
            #region Internal
            
            void UpdateHotBarItem()
            {
                for (var i = 0; i < _localPlayerInventory.Count; i++) UpdateHotBarElement(i, _localPlayerInventory[i]);
            }
            
            void UpdateHotBarElement(int slot, IItemStack item)
            {
                //スロットが一番下の段もしくはメインインベントリの範囲外の時はスルー
                var c = PlayerInventoryConst.MainInventoryColumns;
                var r = PlayerInventoryConst.MainInventoryRows;
                var startHotBarSlot = c * (r - 1);
                
                if (slot < startHotBarSlot || PlayerInventoryConst.MainInventorySize <= slot) return;
                
                var viewData = ClientContext.ItemImageContainer.GetItemView(item.Id);
                slot -= startHotBarSlot;
                hotBarItems[slot].SetItem(viewData, item.Count);
            }
            
            int SelectedHotBar()
            {
                //キーボード入力で選択
                if (InputManager.UI.HotBar.ReadValue<int>() == 0) return -1;
                
                //キー入力で得られる値は1〜9なので-1する
                var selected = InputManager.UI.HotBar.ReadValue<int>() - 1;
                
                OnSelectHotBar?.Invoke(selected);
                return selected;
            }
            
            
            void UpdateHoldItem(int selectIndex)
            {
                if (_currentGrabItem != null) Destroy(_currentGrabItem.gameObject);
                
                var itemId = _localPlayerInventory[PlayerInventoryConst.HotBarSlotToInventorySlot(selectIndex)].Id;
                
                if (itemId == ItemMaster.EmptyItemId) return;
                
                var itemObjectData = itemObjectContainer.GetItemPrefab(itemId);
                if (itemObjectData != null)
                {
                    _currentGrabItem = Instantiate(itemObjectData.ItemPrefab);
                    playerGrabItemManager.SetItem(_currentGrabItem, false, itemObjectData.Position, Quaternion.Euler(itemObjectData.Rotation));
                }
            }
            
            #endregion
        }
        
        public event Action<int> OnSelectHotBar;
        
        [Inject]
        public void Construct(ILocalPlayerInventory localPlayerInventory)
        {
            _localPlayerInventory = localPlayerInventory;
        }
        
        private void UpdateSelectedView(int prevIndex, int nextIndex)
        {
            hotBarItems[prevIndex].SetSelect(false);
            hotBarItems[nextIndex].SetSelect(true);
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}