using System;
using System.Collections.Generic;
using System.Linq;
using ClassLibrary;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Element;
using Client.Input;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.Inventory.Main
{
    /// <summary>
    ///     TODO フラグ管理をステートベースに変換する
    /// </summary>
    public class PlayerInventoryViewController : MonoBehaviour
    {
        [SerializeField] private GameObject mainInventoryObject;
        
        [SerializeField] private List<ItemSlotObject> mainInventorySlotObjects;
        [SerializeField] private ItemSlotObject grabInventorySlotObject;
        
        public Transform SubInventoryParent => subInventoryParent.transform;
        [SerializeField] private Transform subInventoryParent;
        
        //現在スプリットドラッグしているスロットのリスト
        private readonly List<ItemSplitDragSlot> _itemSplitDraggedSlots = new();
        
        private readonly List<IDisposable> _subInventorySlotUIEventUnsubscriber = new();
        
        //ドラッグ中のアイテムをドラッグする前のGrabインベントリ
        private IItemStack _grabInventoryBeforeDrag;
        private bool _isItemOneDragging;
        private bool _isItemSplitDragging;
        
        [Inject] private LocalPlayerInventoryController _playerInventory;
        
        private ISubInventory _subInventory;
        
        private bool IsGrabItem => _playerInventory.GrabInventory.Id != ItemMaster.EmptyItemId;
        
        private void Awake()
        {
            foreach (var mainInventorySlotObject in mainInventorySlotObjects) mainInventorySlotObject.OnPointerEvent.Subscribe(ItemSlotUIEvent);
        }
        
        private void Update()
        {
            InventoryViewUpdate();
        }
        
        public void SetSubInventory(ISubInventory subInventory)
        {
            foreach (var disposable in _subInventorySlotUIEventUnsubscriber) disposable.Dispose();
            
            _subInventorySlotUIEventUnsubscriber.Clear();
            _subInventory = subInventory;
            _playerInventory.SetSubInventory(subInventory);
            foreach (var sub in _subInventory.SubInventorySlotObjects) _subInventorySlotUIEventUnsubscriber.Add(sub.OnPointerEvent.Subscribe(ItemSlotUIEvent));
        }
        
        private void ItemSlotUIEvent((ItemSlotObject slotObject, ItemUIEventType itemUIEvent) eventProperty)
        {
            var (slotObject, itemUIEvent) = eventProperty;
            var index = mainInventorySlotObjects.IndexOf(slotObject);
            if (index == -1)
                index = mainInventorySlotObjects.Count + _subInventory.SubInventorySlotObjects.IndexOf(slotObject);
            
            if (index == -1) throw new Exception("slot index not found");
            switch (itemUIEvent)
            {
                case ItemUIEventType.LeftClickDown:
                    LeftClickDown(index);
                    break;
                case ItemUIEventType.RightClickDown:
                    RightClickDown(index);
                    break;
                case ItemUIEventType.LeftClickUp:
                    LeftClickUp(index);
                    break;
                case ItemUIEventType.RightClickUp:
                    RightClickUp(index);
                    break;
                case ItemUIEventType.CursorEnter:
                    CursorEnter(index);
                    break;
                case ItemUIEventType.DoubleClick:
                    DoubleClick(index);
                    break;
                case ItemUIEventType.CursorExit: break;
                case ItemUIEventType.CursorMove: break;
                default: throw new ArgumentOutOfRangeException(nameof(itemUIEvent), itemUIEvent, null);
            }
        }
        
        
        private void DoubleClick(int slotIndex)
        {
            if (_isItemSplitDragging || _isItemOneDragging) return;
            
            
            IItemStack collectTargetItem;
            LocalMoveInventoryType fromType;
            int fromSlot;
            if (IsGrabItem)
            {
                collectTargetItem = _playerInventory.GrabInventory;
                fromType = LocalMoveInventoryType.Grab;
                fromSlot = 0;
            }
            else
            {
                collectTargetItem = _playerInventory.LocalPlayerInventory[slotIndex];
                fromType = LocalMoveInventoryType.MainOrSub;
                fromSlot = slotIndex;
            }
            
            var collectTargetSotIndex = _playerInventory.LocalPlayerInventory.Select((item, index) => new { item, index }).Where(i => i.item.Id == collectTargetItem.Id).OrderBy(i => i.item.Count).Select(i => i.index).ToList();
            
            //一つのスロットに集める場合は集める先のスロットのインデックスをターゲットから除外する
            if (!IsGrabItem) collectTargetSotIndex.Remove(slotIndex);
            
            foreach (var index in collectTargetSotIndex)
            {
                var added = collectTargetItem.AddItem(_playerInventory.LocalPlayerInventory[index]);
                
                //アイテムを何個移したのかを計算
                var collectItemCount = _playerInventory.LocalPlayerInventory[index].Count - added.RemainderItemStack.Count;
                _playerInventory.MoveItem(LocalMoveInventoryType.MainOrSub, index, fromType, fromSlot, collectItemCount);
                
                collectTargetItem = added.ProcessResultItemStack;
                
                //足したあまりがあるということはスロットにそれ以上入らないということなので、ここで処理を終了する
                if (added.RemainderItemStack.Count != 0) break;
            }
        }
        
        private void CursorEnter(int slotIndex)
        {
            if (_isItemSplitDragging)
                SplitDraggingItem(slotIndex, false);
            else if (_isItemOneDragging)
                //ドラッグ中の時はマウスカーソルが乗ったスロットをドラッグされたと判定する
                PlaceOneItem(slotIndex);
        }
        
        private void RightClickUp(int slotIndex)
        {
            if (_isItemOneDragging) _isItemOneDragging = false;
        }
        
        private void LeftClickUp(int slotIndex)
        {
            //左クリックを離したときはドラッグ中のスロットを解除する
            if (_isItemSplitDragging)
            {
                SplitDraggingItem(slotIndex, true);
                _itemSplitDraggedSlots.Clear();
                _isItemSplitDragging = false;
            }
        }
        
        
        private void RightClickDown(int slotIndex)
        {
            if (IsGrabItem)
            {
                //アイテムを持っている時に右クリックするとアイテム1個だけ置く処理
                PlaceOneItem(slotIndex);
                _isItemOneDragging = true;
            }
            else
            {
                //アイテムを持ってない時に右クリックするとアイテムを半分とる処理
                
                //空スロットの時はアイテムを持たない
                var item = _playerInventory.LocalPlayerInventory[slotIndex];
                if (item.Id == ItemMaster.EmptyItemId) return;
                
                var halfItemCount = item.Count / 2;
                
                _playerInventory.MoveItem(LocalMoveInventoryType.MainOrSub, slotIndex, LocalMoveInventoryType.Grab, 0, halfItemCount);
            }
        }
        
        private void LeftClickDown(int slotIndex)
        {
            if (IsGrabItem)
            {
                var isSlotEmpty = _playerInventory.LocalPlayerInventory[slotIndex].Id == ItemMaster.EmptyItemId;
                
                if (isSlotEmpty)
                {
                    //アイテムを持っている時に左クリックするとアイテムを置くもしくは置き換える処理
                    _isItemSplitDragging = true;
                    _grabInventoryBeforeDrag = _playerInventory.GrabInventory;
                    SplitDraggingItem(slotIndex, false);
                }
                else
                {
                    _playerInventory.MoveItem(LocalMoveInventoryType.Grab, 0, LocalMoveInventoryType.MainOrSub, slotIndex, _playerInventory.GrabInventory.Count);
                }
                
                return;
            }
            
            if (InputManager.UI.ItemDirectMove.GetKey)
            {
                //シフト（デフォルト）＋クリックでメイン、サブのアイテム移動を直接やる処理
                DirectMove(slotIndex);
            }
            else
            {
                var slotItemCount = _playerInventory.LocalPlayerInventory[slotIndex].Count;
                //アイテムを持ってない時に左クリックするとアイテムを取る処理
                _playerInventory.MoveItem(LocalMoveInventoryType.MainOrSub, slotIndex, LocalMoveInventoryType.Grab, 0, slotItemCount);
            }
        }
        
        
        private void PlaceOneItem(int slotIndex)
        {
            var oneItem = ServerContext.ItemStackFactory.Create(_playerInventory.GrabInventory.Id, 1);
            var currentItem = _playerInventory.LocalPlayerInventory[slotIndex];
            
            //追加できない場合はスキップ
            if (!currentItem.IsAllowedToAdd(oneItem)) return;
            
            //アイテムを追加する
            _playerInventory.MoveItem(LocalMoveInventoryType.Grab, 0, LocalMoveInventoryType.MainOrSub, slotIndex, 1);
            
            //Grabインベントリがなくなったらドラッグを終了する
            if (_playerInventory.GrabInventory.Count == 0)
                _isItemOneDragging = false;
        }
        
        private void SplitDraggingItem(int slotIndex, bool isMoveSendData)
        {
            if (!_playerInventory.LocalPlayerInventory[slotIndex].IsAllowedToAddWithRemain(_playerInventory.GrabInventory)) return;
            
            // まだスロットをドラッグしてない時
            var doNotDragging = !_itemSplitDraggedSlots.Exists(i => i.Slot == slotIndex);
            // アイテムがない時か、同じアイテムがあるとき
            var isNotSlotOrSameItem = _playerInventory.LocalPlayerInventory[slotIndex].Id == ItemMaster.EmptyItemId || _playerInventory.LocalPlayerInventory[slotIndex].Id == _grabInventoryBeforeDrag.Id;
            
            // まだスロットをドラッグしてない時 か アイテムがない時か、同じアイテムがあるとき
            if (doNotDragging && isNotSlotOrSameItem)
            {
                //ドラッグ中のアイテムに設定
                _itemSplitDraggedSlots.Add(new ItemSplitDragSlot(slotIndex, _playerInventory.LocalPlayerInventory[slotIndex]));
            }
            
            //一度Grabインベントリをリセットする
            _playerInventory.SetGrabItem(_grabInventoryBeforeDrag);
            foreach (var itemSplit in _itemSplitDraggedSlots) _playerInventory.SetMainItem(itemSplit.Slot, itemSplit.BeforeDragItem);
            
            //1スロットあたりのアイテム数
            var grabItem = _playerInventory.GrabInventory;
            var dragItemCount = grabItem.Count / _itemSplitDraggedSlots.Count;
            //余っているアイテム数
            var remainItemNum = grabItem.Count - dragItemCount * _itemSplitDraggedSlots.Count;
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            foreach (var dragSlot in _itemSplitDraggedSlots)
            {
                //ドラッグ中のスロットにアイテムを加算する
                var addedItem = dragSlot.BeforeDragItem.AddItem(itemStackFactory.Create(grabItem.Id, dragItemCount));
                var moveItemCount = addedItem.ProcessResultItemStack.Count - dragSlot.BeforeDragItem.Count;
                
                _playerInventory.MoveItem(LocalMoveInventoryType.Grab, 0, LocalMoveInventoryType.MainOrSub, dragSlot.Slot, moveItemCount, isMoveSendData);
                //余ったアイテムを加算する
                remainItemNum += addedItem.RemainderItemStack.Count;
            }
            
            //あまりのアイテムをGrabインベントリに設定する
            _playerInventory.SetGrabItem(itemStackFactory.Create(grabItem.Id, remainItemNum));
        }
        
        
        private void DirectMove(int slotIndex)
        {
            //そのスロットがメインインベントリかサブインベントリを判定する
            var isMain = slotIndex < PlayerInventoryConst.MainInventorySize;
            
            var startIndex = isMain ? 0 : PlayerInventoryConst.MainInventorySize;
            var endIndex = isMain ? PlayerInventoryConst.MainInventorySize : PlayerInventoryConst.MainInventorySize + _subInventory.Count;
            for (var i = startIndex; i < endIndex; i++)
            {
                _playerInventory.MoveItem(LocalMoveInventoryType.MainOrSub, slotIndex, LocalMoveInventoryType.MainOrSub, i, _playerInventory.LocalPlayerInventory[slotIndex].Count);
                //アイテムがなくなったら終了する
                if (_playerInventory.LocalPlayerInventory[slotIndex].Count == 0) break;
            }
        }
        
        public void SetActive(bool isActive)
        {
            mainInventoryObject.SetActive(isActive);
        }
        
        private void InventoryViewUpdate()
        {
            for (var i = 0; i < _playerInventory.LocalPlayerInventory.Count; i++)
            {
                var item = _playerInventory.LocalPlayerInventory[i];
                var itemView = ClientContext.ItemImageContainer.GetItemView(item.Id);
                
                if (i < mainInventorySlotObjects.Count)
                {
                    mainInventorySlotObjects[i].SetItem(itemView, item.Count);
                }
                else
                {
                    var subIndex = i - mainInventorySlotObjects.Count;
                    _subInventory.SubInventorySlotObjects[subIndex].SetItem(itemView, item.Count);
                }
            }
            
            grabInventorySlotObject.SetActive(IsGrabItem);
            var garbItemView = ClientContext.ItemImageContainer.GetItemView(_playerInventory.GrabInventory.Id);
            grabInventorySlotObject.SetItem(garbItemView, _playerInventory.GrabInventory.Count);
        }
    }
    
    public class ItemSplitDragSlot
    {
        public ItemSplitDragSlot(int slot, IItemStack beforeDragItem)
        {
            Slot = slot;
            BeforeDragItem = beforeDragItem;
        }
        
        public int Slot { get; }
        public IItemStack BeforeDragItem { get; }
    }
}