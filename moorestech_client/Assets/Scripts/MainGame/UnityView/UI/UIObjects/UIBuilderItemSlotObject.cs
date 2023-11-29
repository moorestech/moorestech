using System;
using Core.Const;
using MainGame.Basic.UI;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Util;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.UIObjects
{
    public class UIBuilderItemSlotObject : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerClickHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [SerializeField] private Image image;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private UIEnterExplainerController uiEnterExplainerController;

        private bool _onPointing;


        public ItemViewData ItemViewData { get; private set; }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (2 == eventData.clickCount && eventData.button == PointerEventData.InputButton.Left) OnDoubleClick?.Invoke(this);
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

        public void OnPointerEnter(PointerEventData eventData)
        {
            _onPointing = true;
            OnCursorEnter?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _onPointing = false;
            OnCursorExit?.Invoke(this);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            OnCursorMove?.Invoke(this);
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


        public event Action<UIBuilderItemSlotObject> OnRightClickDown;
        public event Action<UIBuilderItemSlotObject> OnLeftClickDown;

        public event Action<UIBuilderItemSlotObject> OnRightClickUp;
        public event Action<UIBuilderItemSlotObject> OnLeftClickUp;
        public event Action<UIBuilderItemSlotObject> OnCursorEnter;
        public event Action<UIBuilderItemSlotObject> OnCursorExit;
        public event Action<UIBuilderItemSlotObject> OnDoubleClick;
        public event Action<UIBuilderItemSlotObject> OnCursorMove;


        public void SetItem(ItemViewData itemView, int count)
        {
            ItemViewData = itemView;
            image.sprite = itemView.ItemImage;

            countText.text = count == 0 ? string.Empty : count.ToString();

            if (itemView.ItemId == ItemConst.EmptyItemId)
            {
                uiEnterExplainerController.DisplayEnable(false);
            }
            else
            {
                uiEnterExplainerController.SetText(itemView.ItemName);
                uiEnterExplainerController.DisplayEnable(true);
            }
        }

        public RectTransformReadonlyData GetRectTransformData()
        {
            return new RectTransformReadonlyData(transform as RectTransform);
        }
    }
}