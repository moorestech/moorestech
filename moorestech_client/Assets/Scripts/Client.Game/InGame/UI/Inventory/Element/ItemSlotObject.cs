using System;
using Client.Game.InGame.UI.Util;
using Client.Mod.Texture;
using Core.Const;
using Core.Master;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Element
{
    public class ItemSlotObject : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerClickHandler, IPointerExitHandler, IPointerMoveHandler
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
        [SerializeField] private UIEnterExplainerController uiEnterExplainerController;
        
        private bool _onPointing;
        
        public ItemViewData ItemViewData { get; private set; }
        
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
        
        
        public void SetItem(ItemViewData itemView, int count)
        {
            ItemViewData = itemView;
            
            countText.text = count != 0 ? count.ToString() : string.Empty;
            
            if (itemView == null || itemView.ItemId == ItemMaster.EmptyItemId)
            {
                itemImage.gameObject.SetActive(false);
                
                uiEnterExplainerController.DisplayEnable(false);
            }
            else
            {
                itemImage.gameObject.SetActive(true);
                itemImage.sprite = itemView.ItemImage;
                
                uiEnterExplainerController.SetText($"{itemView.ItemName}\n<size=25>ID:{itemView.ItemId}</size>", false);
                uiEnterExplainerController.DisplayEnable(true);
            }
        }
        
        public void SetGrayOut(bool active)
        {
            grayOutImage.SetActive(active);
        }
        
        public void SetFrame(ItemSlotFrameType frameType)
        {
            normalFrame.SetActive(frameType == ItemSlotFrameType.Normal);
            machineSlotFrame.SetActive(frameType == ItemSlotFrameType.MachineSlot);
            craftRecipeFrame.SetActive(frameType == ItemSlotFrameType.CraftRecipe);
        }
        
        public void SetItemSlotType(ItemSlotType slotType)
        {
            normalItemSlotObject.SetActive(slotType == ItemSlotType.Normal);
            noneCrossObject.SetActive(slotType == ItemSlotType.NoneCross);
        }
        
        public void SetHotBarSelect(bool active)
        {
            hotBarSelect.SetActive(active);
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
        
        #region PointerEvents
        
        public IObservable<(ItemSlotObject, ItemUIEventType)> OnPointerEvent => _onPointerEvent;
        private readonly Subject<(ItemSlotObject, ItemUIEventType)> _onPointerEvent = new();
        
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
        
        private void OnInvokeOtherEvent((ItemSlotObject, ItemUIEventType) data)
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
    
    public enum ItemSlotType
    {
        Normal, // 通常のアイテム表示
        NoneCross, // アイテムが何もないクロス表示
    }
    
    public enum ItemSlotFrameType
    {
        Normal,
        MachineSlot,
        CraftRecipe,
    }
}