using System;
using System.Collections.Generic;
using System.Linq;
using Core.Const;
using Core.Item;
using Cysharp.Threading.Tasks;
using MainGame.UnityView.Control;
using MainGame.UnityView.UI.UIObjects;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using UnityEngine;

namespace MainGame.UnityView.UI.Inventory
{
    public class PlayerInventoryController : MonoBehaviour
    {
        [SerializeField] private List<UIBuilderItemSlotObject> mainInventorySlotObjects;
        
        private ISubInventoryController _subInventoryController;

        private LocalPlayerInventoryDataController _playerInventory;



        private bool IsGrabbed => _playerInventory.GrabInventory.Id == ItemConst.EmptyItemId;
        
        
        private bool _isItemSplitDragging;
        private bool _isItemOneDragging;
        
        
        public void SetSubInventory(ISubInventoryController subInventoryController)
        {
            _subInventoryController = subInventoryController;
        }
        
        
        
        
        private void DoubleClick(int slotIndex)
        {
            if (_isItemSplitDragging || _isItemOneDragging) return;


            IItemStack collectTargetItem;
            if (IsGrabbed)
                collectTargetItem = _playerInventory.GrabInventory;
            else
                collectTargetItem = _playerInventory.AllInventoryItems[slotIndex];
            
            
            var collectTargetSotIndex = _playerInventory.AllInventoryItems.
                Select((item, index) => new { item, index }).
                Where(i => i.item.Id == collectTargetItem.Id).
                OrderBy(i => i.item.Count).
                Select(i => i.index).ToList();
            
            //一つのスロットに集める場合は集める先のスロットのインデックスをターゲットから除外する
            if (!IsGrabbed)
            {
                collectTargetSotIndex.Remove(slotIndex);
            }
            LocalMoveInventoryType
            
            
            foreach (var index in collectTargetSotIndex)
            {
                var added = collectTargetItem.AddItem(_playerInventory.AllInventoryItems[index]);

                //アイテムを何個移したのかを計算
                var collectItemCount = _playerInventory.AllInventoryItems[index].Count - added.RemainderItemStack.Count;
                _playerInventory.MoveItem(

                collectTargetItem = added.ProcessResultItemStack;

                //足したあまりがあるということはスロットにそれ以上入らないということなので、ここで処理を終了する
                if (added.RemainderItemStack.Count != 0) break;
            }
            
            
        }

        private void CursorEnter(int slotIndex)
        {
            if (_isItemSplitDragging)
                //ドラッグ中の時はマウスカーソルが乗ったスロットをドラッグされたと判定する
                ItemSplitDragSlot(slotIndex);
            else if (_isItemOneDragging)
                PlaceOneItem(slotIndex);
        }

        private void RightClickUp(int slotIndex)
        {
            if (_isItemSplitDragging) ItemOneDragEnd();
        }

        private void LeftClickUp(int slotIndex)
        {
            //左クリックを離したときはドラッグ中のスロットを解除する
            if (_isItemSplitDragging) ItemSplitDragEndSlot(slotIndex);
        }


        private void RightClickDown(int slotIndex)
        {
            if (IsGrabbed)
                //アイテムを持っている時に右クリックするとアイテム1個だけ置く処理
                PlaceOneItem(slotIndex);
            else
                //アイテムを持ってない時に右クリックするとアイテムを半分とる処理
                GrabbedHalfItem(slotIndex);
        }

        private void LeftClickDown(int slotIndex)
        {
            if (IsGrabbed)
            {
                //アイテムを持っている時に左クリックするとアイテムを置くもしくは置き換える処理
                PlaceItem(slotIndex);
                return;
            }

            if (InputManager.UI.ItemDirectMove.GetKey)
                //シフト（デフォルト）＋クリックでメイン、サブのアイテム移動を直接やる処理
                DirectMoveItem(slotIndex);
            else
                //アイテムを持ってない時に左クリックするとアイテムを取る処理
                GrabbedItem(slotIndex);
        }

        private void GrabbedItem(int slotIndex)
        {
            throw new NotImplementedException();
        }

        private void PlaceItem(int slotIndex)
        {
            throw new NotImplementedException();
        }


        private void CollectGrabbedItem()
        {
            throw new NotImplementedException();
        }

        private void CollectSlotItem(int slotIndex)
        {
            throw new NotImplementedException();
        }

        private void ItemSplitDragSlot(int slotIndex)
        {
            
        }

        private void PlaceOneItem(int slotIndex)
        {
            
        }

        private void ItemOneDragEnd()
        {
            
        }
        private void ItemSplitDragEndSlot(int slotIndex)
        {
            throw new NotImplementedException();
        }
        private void GrabbedHalfItem(int slotIndex)
        {
            throw new NotImplementedException();
        }
        
        private void DirectMoveItem(int slotIndex)
        {
            throw new NotImplementedException();
        }




        private static void MoveItem(int fromSlot,ItemMoveInventoryType fromType,int toSlot,ItemMoveInventoryType toType)
        {
            
        }
    }
}