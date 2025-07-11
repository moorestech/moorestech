using System;
using Client.Game.InGame.UI.Tooltip;
using Core.Master;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Common
{
    public class CommonSlotView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerClickHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [SerializeField] private Image itemImage;
        
        [SerializeField] private GameObject normalFrame;
        [SerializeField] private GameObject machineSlotFrame;
        [SerializeField] private GameObject craftRecipeFrame;
        
        [SerializeField] private GameObject hotBarSelect;
        
        [SerializeField] private GameObject grayOutImage;
        [SerializeField] private GameObject hoverImage; // TODO 後で対応
        [SerializeField] private GameObject clickImage; // TODO 後で対応
        
        [SerializeField] private GameObject normalItemSlotObject;
        [SerializeField] private GameObject noneCrossObject;
        
        [SerializeField] private TMP_Text countText;
        [SerializeField] private UGuiTooltipTarget uGuiTooltipTarget;
        
        private bool _onPointing;
        
        private void Awake()
        {
            // default true
            uGuiTooltipTarget.DisplayEnable(true);
            
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
        
        
        public void SetView(Sprite sprite, string count, string toolTipText = null)
        {
            countText.text = count;
            
            itemImage.gameObject.SetActive(true);
            itemImage.sprite = sprite;
            
            if (toolTipText != null)
            {
                uGuiTooltipTarget.SetText(toolTipText, false);
                uGuiTooltipTarget.DisplayEnable(true);
            }
        }
        
        public void SetViewClear()
        {
            countText.text = string.Empty;
            itemImage.gameObject.SetActive(false);
            uGuiTooltipTarget.DisplayEnable(false);
        }
        
        public void SetSlotViewOption(CommonSlotViewOption slotOption)
        {
            if (slotOption.GrayOut != null) SetGrayOut(slotOption.GrayOut.Value);
            if (slotOption.HotBarSelected != null) SetHotBarSelect(slotOption.HotBarSelected.Value);
            if (slotOption.ItemSlotFrameType != null) SetFrame(slotOption.ItemSlotFrameType.Value);
            if (slotOption.ItemSlotType != null) SetItemSlotType(slotOption.ItemSlotType.Value);
            if (slotOption.IsShowToolTip != null) uGuiTooltipTarget.DisplayEnable(slotOption.IsShowToolTip.Value);
            if (slotOption.CountTextFontSize != null) countText.fontSize = slotOption.CountTextFontSize.Value; 
            
            #region Internal
            
            void SetFrame(ItemSlotFrameType frameType)
            {
                normalFrame.SetActive(frameType == ItemSlotFrameType.Normal);
                machineSlotFrame.SetActive(frameType == ItemSlotFrameType.MachineSlot);
                craftRecipeFrame.SetActive(frameType == ItemSlotFrameType.CraftRecipe);
            }
            
            void SetGrayOut(bool active)
            {
                grayOutImage.SetActive(active);
            }
            
            void SetItemSlotType(ItemSlotType slotType)
            {
                normalItemSlotObject.SetActive(slotType == ItemSlotType.Normal);
                noneCrossObject.SetActive(slotType == ItemSlotType.NoneCross);
            }
            
            void SetHotBarSelect(bool active)
            {
                hotBarSelect.SetActive(active);
            }
            
            #endregion
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
        
        #region PointerEvents
        
        public IObservable<(CommonSlotView, ItemUIEventType)> OnPointerEvent => _onPointerEvent;
        private readonly Subject<(CommonSlotView, ItemUIEventType)> _onPointerEvent = new();
        
        public IObservable<CommonSlotView> OnRightClickDown => _onRightClickDown;
        private readonly Subject<CommonSlotView> _onRightClickDown = new();
        public IObservable<CommonSlotView> OnLeftClickDown => _onLeftClickDown;
        private readonly Subject<CommonSlotView> _onLeftClickDown = new();
        public IObservable<CommonSlotView> OnRightClickUp => _onRightClickUp;
        private readonly Subject<CommonSlotView> _onRightClickUp = new();
        public IObservable<CommonSlotView> OnLeftClickUp => _onLeftClickUp;
        private readonly Subject<CommonSlotView> _onLeftClickUp = new();
        public IObservable<CommonSlotView> OnCursorEnter => _onCursorEnter;
        private readonly Subject<CommonSlotView> _onCursorEnter = new();
        public IObservable<CommonSlotView> OnCursorExit => _onCursorExit;
        private readonly Subject<CommonSlotView> _onCursorExit = new();
        public IObservable<CommonSlotView> OnCursorMove => _onCursorMove;
        private readonly Subject<CommonSlotView> _onCursorMove = new();
        public IObservable<CommonSlotView> OnDoubleClick => _onDoubleClick;
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
            _onPointing = true;
            _onPointerEvent.OnNext((this, ItemUIEventType.CursorEnter));
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            _onPointing = false;
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