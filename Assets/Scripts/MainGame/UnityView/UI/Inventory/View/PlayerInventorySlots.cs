using System;
using System.Collections.Generic;
using System.Linq;
using MainGame.Basic.UI;
using MainGame.UnityView.UI.Builder;
using MainGame.UnityView.UI.Builder.BluePrint;
using MainGame.UnityView.UI.Builder.Element;
using MainGame.UnityView.UI.Builder.Unity;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View.SubInventory;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory.View
{
    public class PlayerInventorySlots : MonoBehaviour
    {
        [SerializeField] private List<UIBuilderItemSlotObject> mainInventorySlots;
        [SerializeField] private UIBuilder uiBuilder;
        [SerializeField] private Transform subInventorySlotsParent;
        
        private List<UIBuilderItemSlotObject> _subInventorySlots = new();
        private List<IUIBuilderObject> _subInventoryElementObjects = new();

        public event Action<int> OnRightClickDown;
        public event Action<int> OnLeftClickDown;
        
        public event Action<int> OnRightClickUp;
        public event Action<int> OnLeftClickUp;
        public event Action<int> OnCursorEnter;
        public event Action<int> OnCursorExit;
        public event Action<int> OnCursorMove;
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
                    slot.slot.OnCursorMove += _ => OnCursorMove?.Invoke(slot.index);
                    slot.slot.OnDoubleClick += _ => OnDoubleClick?.Invoke(slot.index);
                });
        }

        public void SetImage(int slot,ItemViewData itemView, int count)
        {
            if (slot < mainInventorySlots.Count)
            {
                mainInventorySlots[slot].SetItem(itemView,count);
            }else if(slot - mainInventorySlots.Count < _subInventorySlots.Count)
            {
                _subInventorySlots[slot - mainInventorySlots.Count].SetItem(itemView,count);
            }
        }



        public void SetSubSlots(SubInventoryViewBluePrint subInventoryViewBluePrint,SubInventoryOptions subInventoryOptions)
        {
            OnSetSubInventory?.Invoke(subInventoryOptions);
            foreach (var subSlot in _subInventoryElementObjects)
            {
                Destroy(((MonoBehaviour)subSlot).gameObject);
            }
            _subInventoryElementObjects.Clear();
            _subInventoryElementObjects = uiBuilder.CreateSlots(subInventoryViewBluePrint,subInventorySlotsParent);

            _subInventorySlots = _subInventoryElementObjects.
                Where(o => o.BluePrintElement.ElementElementType is UIBluePrintElementType.ArraySlot or UIBluePrintElementType.OneSlot).
                Select(o => o as UIBuilderItemSlotObject).ToList();
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
                    slot.slot.OnCursorExit += _ => OnCursorExit?.Invoke(slotIndex);
                    slot.slot.OnCursorMove += _ => OnCursorMove?.Invoke(slotIndex);
                    slot.slot.OnDoubleClick += _ => OnDoubleClick?.Invoke(slotIndex);
                });
        }

        /// <summary>
        /// ブループリントシステムで設定された名前でスロットのRectTransformを取得する
        /// </summary>
        /// <param name="idName">ブループリントで設定された名前</param>
        public RectTransformReadonlyData GetSlotRect(string idName)
        {
            foreach (var slot in _subInventorySlots)
            {
                if (slot.BluePrintElement?.IdName == idName)
                {
                    return new RectTransformReadonlyData(slot.transform as RectTransform);
                }
            }

            //TODO スロット以外の要素も探索するようにする

            return null;
        }
    }
}