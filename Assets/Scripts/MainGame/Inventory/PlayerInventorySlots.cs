using System;
using System.Collections.Generic;
using System.Linq;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;

namespace MainGame.Inventory
{
    public class PlayerInventorySlots : MonoBehaviour
    {
        [SerializeField] private List<InventoryItemSlot> mainInventorySlots;
        
        public event Action<int> OnRightClickDown;
        public event Action<int> OnLeftClickDown;
        
        public event Action<int> OnRightClickUp;
        public event Action<int> OnLeftClickUp;
        public event Action<int> OnCursorEnter;
        public event Action<int> OnDoubleClick;

        private void Awake()
        {
            //メインインベントリのスロットのイベント登録
            mainInventorySlots.
                Select((slot,index) => new{slot,index}).ToList().
                ForEach(slot =>
                {
                    slot.slot.OnRightClickDown += _ => OnRightClickDown?.Invoke(slot.index);
                    slot.slot.OnLeftClickDown += _ => OnLeftClickDown?.Invoke(slot.index);
                    slot.slot.OnRightClickUp += _ => OnRightClickUp?.Invoke(slot.index);
                    slot.slot.OnLeftClickUp += _ => OnLeftClickUp?.Invoke(slot.index);
                    slot.slot.OnCursorEnter += _ => OnCursorEnter?.Invoke(slot.index);
                    slot.slot.OnDoubleClick += _ => OnDoubleClick?.Invoke(slot.index);
                });
        }
        
        
        
        public void SetImage(int slot,ItemViewData itemView, int count)
        {
            if (slot < mainInventorySlots.Count)
            {
                mainInventorySlots[slot].SetItem(itemView,count);
            }
        }
    }
}