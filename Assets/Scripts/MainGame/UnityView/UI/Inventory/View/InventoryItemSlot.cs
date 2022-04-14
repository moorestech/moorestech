using System;
using MainGame.UnityView.UI.Inventory.Element;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Inventory.View
{
    public class InventoryItemSlot: MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IPointerEnterHandler,IPointerClickHandler
    {
        public event Action<InventoryItemSlot> OnRightClickDown;
        public event Action<InventoryItemSlot> OnLeftClickDown;
        
        public event Action<InventoryItemSlot> OnRightClickUp;
        public event Action<InventoryItemSlot> OnLeftClickUp;
        public event Action<InventoryItemSlot> OnCursorEnter;
        public event Action<InventoryItemSlot> OnDoubleClick;
        

        [SerializeField] private Image image;
        [SerializeField] private TMP_Text countText;

        
        public void SetItem(ItemViewData itemView, int count)
        {
            image.sprite = itemView.itemImage;
            
            if (count == 0)
            {
                countText.text = "";
            }
            else
            {
                countText.text = count.ToString();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            switch (eventData.pointerId)
            {
                case -1:
                    OnLeftClickDown?.Invoke(this);
                    break;
                case -2:
                    OnRightClickDown?.Invoke(this);
                    break;
            }
        }
        
        public void OnPointerUp(PointerEventData eventData)
        {
            switch (eventData.pointerId)
            {
                case -1:
                    OnLeftClickUp?.Invoke(this);
                    break;
                case -2:
                    OnRightClickUp?.Invoke(this);
                    break;
            }
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            OnCursorEnter?.Invoke(this);
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if(2 == eventData.clickCount && eventData.pointerId == -1){
                OnDoubleClick?.Invoke(this);
            }
        }
        
        
        
        
        
        /// <summary>
        /// これより下削除予定
        /// </summary>
        public delegate void OnItemSlotClicked(int slotIndex);
        public void SubscribeOnItemSlotClick(OnItemSlotClicked onItemSlotClicked) { }
        public void Construct(int slotIndex) { }
    }
}