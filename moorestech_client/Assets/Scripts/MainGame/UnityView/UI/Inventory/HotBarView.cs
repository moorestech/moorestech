using System;
using System.Collections.Generic;
using Core.Item;
using Game.PlayerInventory.Interface;
using MainGame.UnityView.Control;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.Main;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MainGame.UnityView.UI.Inventory
{
    public class HotBarView : MonoBehaviour
    {
        [SerializeField] private Image selectImage;
        [SerializeField] private List<ItemSlotObject> hotBarSlots;
        
        private ItemImageContainer _itemImageContainer;
        private ILocalPlayerInventory _localPlayerInventory;

        public int SelectIndex { get; private set; }

        public event Action<int> OnSelectHotBar;
        
        [Inject]
        public void Construct(ItemImageContainer itemImageContainer,ILocalPlayerInventory localPlayerInventory)
        {
            _itemImageContainer = itemImageContainer;
            _localPlayerInventory = localPlayerInventory;
            SetSelect(0);
        }

        private void Update()
        {
            UpdateHotBar();
            UpdateSelectedHotBar();


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
                hotBarSlots[slot].SetItem(viewData, item.Count);
            }

            void UpdateSelectedHotBar()
            {
                //キーボード入力で選択
                if (InputManager.UI.HotBar.ReadValue<int>() == 0) return;
            
                //キー入力で得られる値は1〜9なので-1する
                SelectIndex = InputManager.UI.HotBar.ReadValue<int>() - 1;
                SetSelect(SelectIndex);

                OnSelectHotBar?.Invoke(SelectIndex);
            }
            
            #endregion
        }


        public void SetSelect(int selectIndex)
        {
            selectImage.transform.position = hotBarSlots[selectIndex].transform.position;
        }

        public void SetActiveSelectHotBar(bool isActive)
        {
            selectImage.gameObject.SetActive(isActive);
        }
    }
}