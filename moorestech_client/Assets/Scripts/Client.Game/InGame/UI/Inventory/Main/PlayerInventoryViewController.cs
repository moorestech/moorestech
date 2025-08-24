using System;
using System.Collections.Generic;
using System.Linq;
using ClassLibrary;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
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
        
        [SerializeField] private List<ItemSlotView> mainInventorySlotObjects;
        [SerializeField] private ItemSlotView grabInventorySlotView;
        
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
        
        private void ItemSlotUIEvent((ItemSlotView slotObject, ItemUIEventType itemUIEvent) eventProperty)
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
            // 移動するアイテムが空の場合は何もしない
            var sourceItem = _playerInventory.LocalPlayerInventory[slotIndex];
            if (sourceItem.Id == ItemMaster.EmptyItemId) return;

            // サブインベントリの有無を判定
            var hasSubInventory = _subInventory != null && _subInventory.Count > 0;
            
            // 移動元の種類を判定
            var sourceType = GetInventoryType(slotIndex, hasSubInventory);
            
            // 移動先の範囲を決定
            var (startIndex, endIndex) = GetTargetRange(sourceType, hasSubInventory);
            
            // 移動先を探して移動
            TryMoveToSlots(slotIndex, sourceItem, startIndex, endIndex);
            
            #region Internal
            
            InventoryType GetInventoryType(int index, bool hasSub)
            {
                if (hasSub && index >= PlayerInventoryConst.MainInventorySize)
                    return InventoryType.SubInventory;
                
                // ホットバーの判定（36-44のスロット）
                if (index >= (PlayerInventoryConst.MainInventoryRows - 1) * PlayerInventoryConst.MainInventoryColumns)
                    return InventoryType.HotBar;
                
                return InventoryType.MainInventory;
            }
            
            (int start, int end) GetTargetRange(InventoryType source, bool hasSub)
            {
                switch (source)
                {
                    case InventoryType.MainInventory:
                        // メインインベントリから：サブがあればサブへ、なければホットバーへ
                        if (hasSub)
                            return (PlayerInventoryConst.MainInventorySize, PlayerInventoryConst.MainInventorySize + _subInventory.Count);
                        else
                            return ((PlayerInventoryConst.MainInventoryRows - 1) * PlayerInventoryConst.MainInventoryColumns, PlayerInventoryConst.MainInventorySize);
                    
                    case InventoryType.HotBar:
                        // ホットバーから：サブがあればサブへ、なければメインインベントリへ
                        if (hasSub)
                            return (PlayerInventoryConst.MainInventorySize, PlayerInventoryConst.MainInventorySize + _subInventory.Count);
                        else
                            return (0, (PlayerInventoryConst.MainInventoryRows - 1) * PlayerInventoryConst.MainInventoryColumns);
                    
                    case InventoryType.SubInventory:
                        // サブインベントリから：メインインベントリへ（ホットバーを除く）
                        return (0, (PlayerInventoryConst.MainInventoryRows - 1) * PlayerInventoryConst.MainInventoryColumns);
                    
                    default:
                        return (0, 0);
                }
            }
            
            void TryMoveToSlots(int sourceSlot, IItemStack sourceItemStack, int start, int end)
            {
                // まず同じアイテムがあるスロットを探す
                for (var i = start; i < end; i++)
                {
                    if (TryMoveToStackableSlot(sourceSlot, sourceItemStack, i)) return;
                }
                
                // 次に空のスロットを探す
                for (var i = start; i < end; i++)
                {
                    if (TryMoveToEmptySlot(sourceSlot, i)) return;
                }
            }
            
            bool TryMoveToStackableSlot(int sourceSlot, IItemStack sourceItemStack, int targetSlot)
            {
                var targetItem = _playerInventory.LocalPlayerInventory[targetSlot];
                
                // 空のスロットまたは異なるアイテムの場合はスキップ
                if (targetItem.Id == ItemMaster.EmptyItemId || targetItem.Id != sourceItemStack.Id) 
                    return false;
                
                var maxStack = MasterHolder.ItemMaster.GetItemMaster(targetItem.Id).MaxStack;
                if (targetItem.Count >= maxStack) 
                    return false;
                
                var moveCount = _playerInventory.LocalPlayerInventory[sourceSlot].Count;
                _playerInventory.MoveItem(LocalMoveInventoryType.MainOrSub, sourceSlot, LocalMoveInventoryType.MainOrSub, targetSlot, moveCount);
                
                return _playerInventory.LocalPlayerInventory[sourceSlot].Count == 0;
            }
            
            bool TryMoveToEmptySlot(int sourceSlot, int targetSlot)
            {
                var targetItem = _playerInventory.LocalPlayerInventory[targetSlot];
                if (targetItem.Id != ItemMaster.EmptyItemId) 
                    return false;
                
                var moveCount = _playerInventory.LocalPlayerInventory[sourceSlot].Count;
                _playerInventory.MoveItem(LocalMoveInventoryType.MainOrSub, sourceSlot, LocalMoveInventoryType.MainOrSub, targetSlot, moveCount);
                
                return _playerInventory.LocalPlayerInventory[sourceSlot].Count == 0;
            }
            
            #endregion
        }
        
        private enum InventoryType
        {
            MainInventory,
            HotBar,
            SubInventory
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
            
            grabInventorySlotView.SetActive(IsGrabItem);
            var garbItemView = ClientContext.ItemImageContainer.GetItemView(_playerInventory.GrabInventory.Id);
            grabInventorySlotView.SetItem(garbItemView, _playerInventory.GrabInventory.Count);
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