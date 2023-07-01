using System;
using Core.Const;
using MainGame.Basic.UI;
using MainGame.UnityView.UI.Builder.BluePrint;
using MainGame.UnityView.UI.Inventory.Element;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Builder.Unity
{
    public class UIBuilderItemSlotObject: MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IPointerEnterHandler,IPointerClickHandler,IPointerExitHandler,IPointerMoveHandler,
        IUIBuilderObject
    {
        public event Action<UIBuilderItemSlotObject> OnRightClickDown;
        public event Action<UIBuilderItemSlotObject> OnLeftClickDown;
        
        public event Action<UIBuilderItemSlotObject> OnRightClickUp;
        public event Action<UIBuilderItemSlotObject> OnLeftClickUp;
        public event Action<UIBuilderItemSlotObject> OnCursorEnter;
        public event Action<UIBuilderItemSlotObject> OnCursorExit;
        public event Action<UIBuilderItemSlotObject> OnDoubleClick;
        public event Action<UIBuilderItemSlotObject> OnCursorMove;
        

        [SerializeField] private Image image;
        [SerializeField] private TMP_Text countText;

        private InventorySlotElementOptions _slotOptions = new();

        private bool _onPointing;
        
        
        public ItemViewData ItemViewData { get; private set; }
        public IUIBluePrintElement BluePrintElement { get; private set; }
        
        public void Initialize(IUIBluePrintElement bluePrintElement)
        {
            BluePrintElement = bluePrintElement;
        }


        public void SetItem(ItemViewData itemView, int count)
        {
            ItemViewData = itemView;
            image.sprite = itemView.ItemImage;

            countText.text = count == 0 ? string.Empty : count.ToString();
            if (_onPointing && itemView.ItemId != ItemConst.EmptyItemId)
            {
                ItemNameBar.Instance.ShowItemName(itemView.ItemName);
            }
        }

        public void SetSlotOptions(InventorySlotElementOptions slotOptions)
        {
            _slotOptions = slotOptions;
            GetComponent<Button>().enabled = slotOptions.IsButtonEnable;
        }
        
        public RectTransformReadonlyData GetRectTransformData()
        {
            return new RectTransformReadonlyData(transform as RectTransform);
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
            _onPointing = true;
            if (ItemViewData.ItemId != ItemConst.EmptyItemId)
            {
                ItemNameBar.Instance.ShowItemName(ItemViewData.ItemName);
            }

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
            _onPointing = false;
            ItemNameBar.Instance.HideItemName();
            if (!_slotOptions.IsEnableControllerEvent)return;
            
            OnCursorExit?.Invoke(this);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!_slotOptions.IsEnableControllerEvent)return;
            
            OnCursorMove?.Invoke(this);
        }

        private void OnDisable()
        {
            ItemNameBar.Instance.HideItemName();
        }

    }
}