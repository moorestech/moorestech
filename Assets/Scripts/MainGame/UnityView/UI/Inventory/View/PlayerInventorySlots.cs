using System;
using System.Collections.Generic;
using System.Linq;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View.SubInventory;
using MainGame.UnityView.UI.Inventory.View.SubInventory.Element;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory.View
{
    public class PlayerInventorySlots : MonoBehaviour
    {
        [SerializeField] private List<InventoryItemSlot> mainInventorySlots;
        [SerializeField] private SubInventorySlotCreator subInventorySlotCreator;
        [SerializeField] private Transform subInventorySlotsParent;
        
        private List<InventoryItemSlot> _subInventorySlots = new();
        private List<GameObject> _subInventorySlotsObjects = new();

        public event Action<int> OnRightClickDown;
        public event Action<int> OnLeftClickDown;
        
        public event Action<int> OnRightClickUp;
        public event Action<int> OnLeftClickUp;
        public event Action<int> OnCursorEnter;
        public event Action<int> OnCursorExit;
        public event Action<int> OnDoubleClick;
        
        public event Action<SubInventoryOptions> OnSetSubInventory;

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
                    slot.slot.OnCursorExit += _ => OnCursorExit?.Invoke(slot.index);
                    slot.slot.OnDoubleClick += _ => OnDoubleClick?.Invoke(slot.index);
                });
        }

        public void SetImage(int slot,ItemViewData itemView, int count)
        {
            if (slot < mainInventorySlots.Count)
            {
                mainInventorySlots[slot].SetItem(itemView,count);
            }else
            {
                _subInventorySlots[slot - mainInventorySlots.Count].SetItem(itemView,count);
            }
        }



        public void SetSubSlots(SubInventoryViewBluePrint subInventoryViewBluePrint,SubInventoryOptions subInventoryOptions)
        {
            OnSetSubInventory?.Invoke(subInventoryOptions);
            foreach (var subSlot in _subInventorySlotsObjects)
            {
                Destroy(subSlot);
            }
            _subInventorySlots.Clear();
            _subInventorySlotsObjects.Clear();
            
            
            (_subInventorySlots,_subInventorySlotsObjects) = subInventorySlotCreator.CreateSlots(subInventoryViewBluePrint,subInventorySlotsParent);
            _subInventorySlots.
                Select((slot,index) => new{slot,index}).ToList().
                ForEach(slot =>
                {
                    var slotIndex = slot.index + mainInventorySlots.Count;
                    slot.slot.OnRightClickDown += _ => OnRightClickDown?.Invoke(slotIndex);
                    slot.slot.OnLeftClickDown += _ => OnLeftClickDown?.Invoke(slotIndex);
                    slot.slot.OnRightClickUp += _ => OnRightClickUp?.Invoke(slotIndex);
                    slot.slot.OnLeftClickUp += _ => OnLeftClickUp?.Invoke(slotIndex);
                    slot.slot.OnCursorEnter += _ => OnCursorEnter?.Invoke(slotIndex);
                    slot.slot.OnCursorExit += _ => OnCursorExit?.Invoke(slot.index);
                    slot.slot.OnDoubleClick += _ => OnDoubleClick?.Invoke(slotIndex);
                });
        }
    }
}