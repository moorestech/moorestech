using System;
using Core.Const;
using MainGame.Basic.UI;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Util;
using TMPro;
using UniRx;
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
            if (2 == eventData.clickCount && eventData.button == PointerEventData.InputButton.Left) _onUIEvent.OnNext((this,ItemUIEventType.DoubleClick));
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            switch (eventData.button)
            {
                case PointerEventData.InputButton.Left:
                    _onUIEvent.OnNext((this,ItemUIEventType.LeftClickDown));
                    break;
                case PointerEventData.InputButton.Right:
                    _onUIEvent.OnNext((this,ItemUIEventType.RightClickDown));
                    break;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _onPointing = true;
            _onUIEvent.OnNext((this,ItemUIEventType.CursorEnter));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _onPointing = false;
            _onUIEvent.OnNext((this,ItemUIEventType.CursorExit));
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            _onUIEvent.OnNext((this,ItemUIEventType.CursorMove));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            switch (eventData.button)
            {
                case PointerEventData.InputButton.Left:
                    _onUIEvent.OnNext((this,ItemUIEventType.LeftClickUp));
                    break;
                case PointerEventData.InputButton.Right:
                    _onUIEvent.OnNext((this,ItemUIEventType.RightClickUp));
                    break;
            }
        }
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
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }

        public IObservable<(UIBuilderItemSlotObject,ItemUIEventType)> OnUIEvent => _onUIEvent;
        private readonly Subject<(UIBuilderItemSlotObject,ItemUIEventType)> _onUIEvent = new();

        public RectTransformReadonlyData GetRectTransformData()
        {
            return new RectTransformReadonlyData(transform as RectTransform);
        }
    }

    public enum ItemUIEventType
    {
        RightClickDown,
        LeftClickDown,
        RightClickUp,
        LeftClickUp,
        
        CursorEnter,
        CursorExit,
        
        DoubleClick,
        CursorMove,
    }
}