using System;
using Core.Const;
using MainGame.Basic.UI;
using MainGame.ModLoader.Texture;
using MainGame.UnityView.UI.Util;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Inventory.Element
{
    public class ItemSlotObject : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerClickHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [SerializeField] private Image image;
        [SerializeField] private Image grayOutImage;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private UIEnterExplainerController uiEnterExplainerController;
        
        private bool _onPointing;
        
        public ItemViewData ItemViewData { get; private set; }

        #region PointerEvents

        public IObservable<(ItemSlotObject,ItemUIEventType)> OnPointerEvent => _onPointerEvent;
        private readonly Subject<(ItemSlotObject,ItemUIEventType)> _onPointerEvent = new();
        
        public IObservable<ItemSlotObject> OnRightClickDown => _onRightClickDown;
        private readonly Subject<ItemSlotObject> _onRightClickDown = new();
        public IObservable<ItemSlotObject> OnLeftClickDown => _onLeftClickDown;
        private readonly Subject<ItemSlotObject> _onLeftClickDown = new();
        public IObservable<ItemSlotObject> OnRightClickUp => _onRightClickUp;
        private readonly Subject<ItemSlotObject> _onRightClickUp = new();
        public IObservable<ItemSlotObject> OnLeftClickUp => _onLeftClickUp;
        private readonly Subject<ItemSlotObject> _onLeftClickUp = new();
        public IObservable<ItemSlotObject> OnCursorEnter => _onCursorEnter;
        private readonly Subject<ItemSlotObject> _onCursorEnter = new();
        public IObservable<ItemSlotObject> OnCursorExit => _onCursorExit;
        private readonly Subject<ItemSlotObject> _onCursorExit = new();
        public IObservable<ItemSlotObject> OnCursorMove => _onCursorMove;
        private readonly Subject<ItemSlotObject> _onCursorMove = new();
        public IObservable<ItemSlotObject> OnDoubleClick => _onDoubleClick;
        private readonly Subject<ItemSlotObject> _onDoubleClick = new();
        
        private void OnInvokeOtherEvent((ItemSlotObject,ItemUIEventType) data)
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
        public void SetItem(ItemViewData itemView, int count)
        {
            ItemViewData = itemView;
            image.sprite = itemView.ItemImage;

            countText.text = count != 0? count.ToString() : string.Empty;

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