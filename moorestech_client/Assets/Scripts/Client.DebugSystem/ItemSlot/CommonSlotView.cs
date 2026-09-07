using System;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Client.DebugSystem.ItemSlot
{
    /// <summary>
    ///     デバッグUI（ItemSelectModal）専用のスロット描画とポインタ入力
    ///     Slot rendering and pointer input used only by the debug UI (ItemSelectModal)
    /// </summary>
    public class CommonSlotView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerClickHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [SerializeField] private Image itemImage;
        
        [SerializeField] private GameObject hoverImage;
        [SerializeField] private GameObject clickImage;
        
        [SerializeField] private TMP_Text countText;
        
        private void Awake()
        {
            OnPointerEvent.Subscribe(OnInvokeOtherEvent).AddTo(this);
            SubscribeHover();
            SubscribeClick();
        }
        
        private void SubscribeHover()
        {
            _onCursorEnter.Subscribe(_ => hoverImage.SetActive(true)).AddTo(this);
            _onCursorExit.Subscribe(_ => hoverImage.SetActive(false)).AddTo(this);
        }
        
        private void SubscribeClick()
        {
            _onLeftClickDown.Subscribe(_ => clickImage.SetActive(true)).AddTo(this);
            _onLeftClickUp.Subscribe(_ => clickImage.SetActive(false)).AddTo(this);
        }
        
        public void SetView(Sprite sprite, string count)
        {
            countText.text = count;
            
            itemImage.gameObject.SetActive(true);
            itemImage.sprite = sprite;
        }
        
        public void SetViewClear()
        {
            countText.text = string.Empty;
            itemImage.gameObject.SetActive(false);
        }
        
        
        #region PointerEvents
        
        public IObservable<(CommonSlotView, ItemUIEventType)> OnPointerEvent => _onPointerEvent;
        private readonly Subject<(CommonSlotView, ItemUIEventType)> _onPointerEvent = new();
        
        public IObservable<CommonSlotView> OnRightClickUp => _onRightClickUp;
        private readonly Subject<CommonSlotView> _onRightClickUp = new();
        private readonly Subject<CommonSlotView> _onRightClickDown = new();
        private readonly Subject<CommonSlotView> _onLeftClickDown = new();
        private readonly Subject<CommonSlotView> _onLeftClickUp = new();
        private readonly Subject<CommonSlotView> _onCursorEnter = new();
        private readonly Subject<CommonSlotView> _onCursorExit = new();
        private readonly Subject<CommonSlotView> _onCursorMove = new();
        private readonly Subject<CommonSlotView> _onDoubleClick = new();
        
        private void OnInvokeOtherEvent((CommonSlotView, ItemUIEventType) data)
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
        
        public void OnPointerClick(PointerEventData eventData)
        {
            if (2 == eventData.clickCount && eventData.button == PointerEventData.InputButton.Left) _onPointerEvent.OnNext((this, ItemUIEventType.DoubleClick));
        }
        
        public void OnPointerDown(PointerEventData eventData)
        {
            switch (eventData.button)
            {
                case PointerEventData.InputButton.Left:
                    _onPointerEvent.OnNext((this, ItemUIEventType.LeftClickDown));
                    break;
                case PointerEventData.InputButton.Right:
                    _onPointerEvent.OnNext((this, ItemUIEventType.RightClickDown));
                    break;
            }
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            _onPointerEvent.OnNext((this, ItemUIEventType.CursorEnter));
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            _onPointerEvent.OnNext((this, ItemUIEventType.CursorExit));
        }
        
        public void OnPointerMove(PointerEventData eventData)
        {
            _onPointerEvent.OnNext((this, ItemUIEventType.CursorMove));
        }
        
        public void OnPointerUp(PointerEventData eventData)
        {
            switch (eventData.button)
            {
                case PointerEventData.InputButton.Left:
                    _onPointerEvent.OnNext((this, ItemUIEventType.LeftClickUp));
                    break;
                case PointerEventData.InputButton.Right:
                    _onPointerEvent.OnNext((this, ItemUIEventType.RightClickUp));
                    break;
            }
        }
        
        #endregion
    }
}
