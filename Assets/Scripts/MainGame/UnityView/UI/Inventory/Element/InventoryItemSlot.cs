using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Inventory.Element
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
            switch (eventData.button)
            {
                case PointerEventData.InputButton.Left:
                    OnLeftClickDown?.Invoke(this);
                    break;
                case PointerEventData.InputButton.Right:
                    OnRightClickDown?.Invoke(this);
                    break;
            }
        }
        
        public void OnPointerUp(PointerEventData eventData)
        {
            switch (eventData.button)
            {
                case PointerEventData.InputButton.Left:
                    OnLeftClickUp?.Invoke(this);
                    break;
                case PointerEventData.InputButton.Right:
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
            if(2 == eventData.clickCount && eventData.button == PointerEventData.InputButton.Left){
                OnDoubleClick?.Invoke(this);
            }
        }
    }
}