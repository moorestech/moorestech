using System;
using System.Collections.Generic;
using Core.Const;
using Core.Item;
using Core.Item.Config;
using Game.PlayerInventory.Interface;
using MainGame.UnityView.Control;
using MainGame.UnityView.Item;
using MainGame.UnityView.Player;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.Main;
using SinglePlay;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MainGame.UnityView.UI.Inventory
{
    public class HotBarView : MonoBehaviour
    {
        public static HotBarView Instance { get; private set; }
        
        [SerializeField] private Image selectImage;
        [SerializeField] private List<ItemSlotObject> hotBarSlots;
        [SerializeField] private ItemObjectContainer itemObjectContainer;
        [SerializeField] private PlayerGrabItemManager playerGrabItemManager;
        
        private ItemImageContainer _itemImageContainer;
        private ILocalPlayerInventory _localPlayerInventory;
        private IItemConfig _itemConfig;

        public int SelectIndex { get; private set; }

        public event Action<int> OnSelectHotBar;
        
        [Inject]
        public void Construct(ItemImageContainer itemImageContainer,ILocalPlayerInventory localPlayerInventory,SinglePlayInterface singlePlayInterface)
        {
            _itemImageContainer = itemImageContainer;
            _localPlayerInventory = localPlayerInventory;
            _itemConfig = singlePlayInterface.ItemConfig;
            Instance = this;
        }

        private void Start()
        {
            SelectIndex = 0;
        }
        GameObject _currentGrabItem = null;

        private void Update()
        {
            UpdateHotBar();
            var selectIndex = SelectedHotBar();
            
            if (selectIndex != -1 && selectIndex != SelectIndex)
            {
                SelectIndex = selectIndex;
                UpdateHoldItem(selectIndex); //アイテムの再生成があるので変化を検知して変更する
            }
            UpdateSelect(SelectIndex);//毎フレームやらないと、なぜか最初の数フレームで正しい位置に来ない

            #region Internal

            void UpdateHotBar()
            {
                for (int i = 0; i < _localPlayerInventory.Count; i++)
                {
                    UpdateHotBarElement(i, _localPlayerInventory[i]);
                }
            }
            
            void UpdateHotBarElement(int slot, IItemStack item)
            {
                //スロットが一番下の段もしくはメインインベントリの範囲外の時はスルー
                var c = PlayerInventoryConst.MainInventoryColumns;
                var r = PlayerInventoryConst.MainInventoryRows;
                var startHotBarSlot = c * (r - 1);

                if (slot < startHotBarSlot || PlayerInventoryConst.MainInventorySize <= slot) return;

                var viewData = _itemImageContainer.GetItemView(item.Id);
                slot -= startHotBarSlot;
                hotBarSlots[slot].SetItem(viewData, item.Count,false);
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
            
            void UpdateSelect(int selectIndex)
            {
                selectImage.transform.position = hotBarSlots[selectIndex].transform.position;
            }
            
            void UpdateHoldItem(int selectIndex)
            {
                var itemId = _localPlayerInventory[PlayerInventoryConst.HotBarSlotToInventorySlot(selectIndex)].Id;
                if (itemId == ItemConst.EmptyItemId) return;
            
                var itemConfig = _itemConfig.GetItemConfig(itemId);

                if(_currentGrabItem != null) Destroy(_currentGrabItem.gameObject);
            
                var itemObjectData = itemObjectContainer.GetItemPrefab(itemConfig.ModId, itemConfig.Name);
                if (itemObjectData != null)
                {
                    _currentGrabItem = Instantiate(itemObjectData.ItemPrefab);
                    playerGrabItemManager.SetItem(_currentGrabItem,false,itemObjectData.Position,Quaternion.Euler(itemObjectData.Rotation));
                }
            }
            
            #endregion
        }


        

        public void SetActiveSelectHotBar(bool isActive)
        {
            selectImage.gameObject.SetActive(isActive);
        }
    }
}