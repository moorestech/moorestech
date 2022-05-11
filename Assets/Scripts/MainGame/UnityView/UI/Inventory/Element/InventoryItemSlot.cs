using System;
using MainGame.UnityView.UI.Inventory.View.SubInventory;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Inventory.Element
{
    public class InventoryItemSlot: MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IPointerEnterHandler,IPointerClickHandler,IPointerExitHandler,IPointerMoveHandler
    {
        public event Action<InventoryItemSlot> OnRightClickDown;
        public event Action<InventoryItemSlot> OnLeftClickDown;
        
        public event Action<InventoryItemSlot> OnRightClickUp;
        public event Action<InventoryItemSlot> OnLeftClickUp;
        public event Action<InventoryItemSlot> OnCursorEnter;
        public event Action<InventoryItemSlot> OnCursorExit;
        public event Action<InventoryItemSlot> OnDoubleClick;
        public event Action<InventoryItemSlot> OnCursorMove;
        

        [SerializeField] private Image image;
        [SerializeField] private TMP_Text countText;

        private InventorySlotElementOptions _slotOptions = new();
        


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

        public void SetSlotOptions(InventorySlotElementOptions slotOptions)
        {
            _slotOptions = slotOptions;
            GetComponent<Button>().enabled = slotOptions.IsButtonEnable;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            switch (eventData.button)
            {
                case PointerEventData.InputButton.Left:
                    _slotOptions.InvokeOnLeftClickDown(this);
                    
                    if (!_slotOptions.IsEnableControllerEvent)return;
                    OnLeftClickDown?.Invoke(this);
                    break;
                case PointerEventData.InputButton.Right:
                    _slotOptions.InvokeOnRightClickDown(this);
                    
                    if (!_slotOptions.IsEnableControllerEvent)return;
                    OnRightClickDown?.Invoke(this);
                    break;
            }
        }
        
        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_slotOptions.IsEnableControllerEvent)return;
            
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
            if (!_slotOptions.IsEnableControllerEvent)return;
            
            OnCursorEnter?.Invoke(this);
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_slotOptions.IsEnableControllerEvent)return;

            if(2 == eventData.clickCount && eventData.button == PointerEventData.InputButton.Left){
                OnDoubleClick?.Invoke(this);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_slotOptions.IsEnableControllerEvent)return;
            
            OnCursorExit?.Invoke(this);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!_slotOptions.IsEnableControllerEvent)return;
            
            OnCursorMove?.Invoke(this);
        }
    }
}