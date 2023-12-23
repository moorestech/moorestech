using System;
using Core.Const;
using MainGame.Basic.UI;
using MainGame.ModLoader.Texture;
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
        [SerializeField] private Image grayOutImage;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private UIEnterExplainerController uiEnterExplainerController;
        
        private bool _onPointing;
        
        public ItemViewData ItemViewData { get; private set; }

        #region PointerEvents

        public IObservable<(UIBuilderItemSlotObject,ItemUIEventType)> OnPointerEvent => _onPointerEvent;
        private readonly Subject<(UIBuilderItemSlotObject,ItemUIEventType)> _onPointerEvent = new();
        
        public IObservable<UIBuilderItemSlotObject> OnRightClickDown => _onRightClickDown;
        private readonly Subject<UIBuilderItemSlotObject> _onRightClickDown = new();
        public IObservable<UIBuilderItemSlotObject> OnLeftClickDown => _onLeftClickDown;
        private readonly Subject<UIBuilderItemSlotObject> _onLeftClickDown = new();
        public IObservable<UIBuilderItemSlotObject> OnRightClickUp => _onRightClickUp;
        private readonly Subject<UIBuilderItemSlotObject> _onRightClickUp = new();
        public IObservable<UIBuilderItemSlotObject> OnLeftClickUp => _onLeftClickUp;
        private readonly Subject<UIBuilderItemSlotObject> _onLeftClickUp = new();
        public IObservable<UIBuilderItemSlotObject> OnCursorEnter => _onCursorEnter;
        private readonly Subject<UIBuilderItemSlotObject> _onCursorEnter = new();
        public IObservable<UIBuilderItemSlotObject> OnCursorExit => _onCursorExit;
        private readonly Subject<UIBuilderItemSlotObject> _onCursorExit = new();
        public IObservable<UIBuilderItemSlotObject> OnCursorMove => _onCursorMove;
        private readonly Subject<UIBuilderItemSlotObject> _onCursorMove = new();
        public IObservable<UIBuilderItemSlotObject> OnDoubleClick => _onDoubleClick;
        private readonly Subject<UIBuilderItemSlotObject> _onDoubleClick = new();
        
        private void OnInvokeOtherEvent((UIBuilderItemSlotObject,ItemUIEventType) data)
        {
            var type = data.Item2;
            var slot = data.Item1;
            switch (type)
            {
                case ItemUIEventType.RightClickDown:
                    _onRightClickDown.OnNext(slot);
                    break;
                case ItemUIEventType.LeftClickDown:
                    _onLeftClickDown.OnNext(slot);
                    break;
                case ItemUIEventType.RightClickUp:
                    _onRightClickUp.OnNext(slot);
                    break;
                case ItemUIEventType.LeftClickUp:
                    _onLeftClickUp.OnNext(slot);
                    break;
                case ItemUIEventType.CursorEnter:
                    _onCursorEnter.OnNext(slot);
                    break;
                case ItemUIEventType.CursorExit:
                    _onCursorExit.OnNext(slot);
                    break;
                case ItemUIEventType.CursorMove:
                    _onCursorMove.OnNext(slot);
                    break;
                case ItemUIEventType.DoubleClick:
                    _onDoubleClick.OnNext(slot);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        #endregion

        private void Awake()
        {
            OnPointerEvent.Subscribe(OnInvokeOtherEvent);
        }


        public void OnPointerClick(PointerEventData eventData)
        {
            if (2 == eventData.clickCount && eventData.button == PointerEventData.InputButton.Left) _onPointerEvent.OnNext((this,ItemUIEventType.DoubleClick));
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            switch (eventData.button)
            {
                case PointerEventData.InputButton.Left:
                    _onPointerEvent.OnNext((this,ItemUIEventType.LeftClickDown));
                    break;
                case PointerEventData.InputButton.Right:
                    _onPointerEvent.OnNext((this,ItemUIEventType.RightClickDown));
                    break;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _onPointing = true;
            _onPointerEvent.OnNext((this,ItemUIEventType.CursorEnter));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _onPointing = false;
            _onPointerEvent.OnNext((this,ItemUIEventType.CursorExit));
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            _onPointerEvent.OnNext((this,ItemUIEventType.CursorMove));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            switch (eventData.button)
            {
                case PointerEventData.InputButton.Left:
                    _onPointerEvent.OnNext((this,ItemUIEventType.LeftClickUp));
                    break;
                case PointerEventData.InputButton.Right:
                    _onPointerEvent.OnNext((this,ItemUIEventType.RightClickUp));
                    break;
            }
        }
        public void SetItem(ItemViewData itemView, int count,bool displayCountText)
        {
            ItemViewData = itemView;
            image.sprite = itemView.ItemImage;

            countText.text = displayCountText ? string.Empty : count.ToString();

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
        public void SetGrayOut(bool isGrayOut)
        {
            grayOutImage.gameObject.SetActive(isGrayOut);
        }


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
        CursorMove,
        
        DoubleClick,
    }
}