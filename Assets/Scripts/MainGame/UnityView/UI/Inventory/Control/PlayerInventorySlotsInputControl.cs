using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.Inventory.Control
{
    public class PlayerInventorySlotsInputControl : MonoBehaviour
    {
        [SerializeField] private PlayerInventorySlots playerInventorySlots;

        private PlayerInventoryModelController _playerInventoryModelController;

        [Inject]
        public void Construct(PlayerInventoryModelController playerInventoryModelController)
        {
            _playerInventoryModelController = playerInventoryModelController;
        }
        
        private void Awake()
        {
            playerInventorySlots.OnLeftClickDown += LeftClickDown;
            playerInventorySlots.OnRightClickDown += RightClickDown;
            playerInventorySlots.OnLeftClickUp += LeftClickUp;
            playerInventorySlots.OnRightClickUp += RightClickUp;
            playerInventorySlots.OnCursorEnter += CursorEnter;
            playerInventorySlots.OnDoubleClick += DoubleClick;
            
        }

        private void DoubleClick(int slotIndex)
        {
            if (_playerInventoryModelController.IsGrabbed)
            {
                _playerInventoryModelController.CollectGrabbedItem();
            }
            else
            {
                _playerInventoryModelController.CollectSlotItem(slotIndex);
            }
        }

        private void CursorEnter(int slotIndex)
        {
            if (_playerInventoryModelController.IsItemSplitDragging)
            {
                //ドラッグ中の時はマウスカーソルが乗ったスロットをドラッグされたと判定する
                _playerInventoryModelController.ItemSplitDragSlot(slotIndex);
            }
            else if (_playerInventoryModelController.IsItemOneDragging)
            {
                _playerInventoryModelController.PlaceOneItem(slotIndex);
            }
        }

        private void RightClickUp(int slotIndex)
        {
            if (_playerInventoryModelController.IsItemOneDragging)
            {
                _playerInventoryModelController.ItemOneDragEnd();
            }
        }

        private void LeftClickUp(int slotIndex)
        {
            //左クリックを離したときはドラッグ中のスロットを解除する
            if (_playerInventoryModelController.IsItemSplitDragging)
            {
                _playerInventoryModelController.ItemSplitDragEndSlot(slotIndex);
            }
        }

        private void RightClickDown(int slotIndex)
        {
            if (_playerInventoryModelController.IsGrabbed)
            {
                //アイテムを持っている時に右クリックするとアイテム1個だけ置く処理
                _playerInventoryModelController.PlaceOneItem(slotIndex);
            }
            else
            {
                //アイテムを持ってない時に右クリックするとアイテムを半分とる処理
                _playerInventoryModelController.GrabbedHalfItem(slotIndex);
            }
            
        }
        
        private void LeftClickDown(int slotIndex)
        {
            if (_playerInventoryModelController.IsGrabbed)
            {
                //アイテムを持っている時に左クリックするとアイテムを置くもしくは置き換える処理
                _playerInventoryModelController.PlaceItem(slotIndex);
            }
            else
            {
                //アイテムを持ってない時に左クリックするとアイテムを取る処理
                _playerInventoryModelController.GrabbedItem(slotIndex);
            }
        }
        
        
    }
}