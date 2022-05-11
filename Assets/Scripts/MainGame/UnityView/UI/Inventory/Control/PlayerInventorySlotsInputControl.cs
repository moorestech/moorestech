using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.Inventory.Control
{
    public class PlayerInventorySlotsInputControl : MonoBehaviour
    {
        [SerializeField] private PlayerInventorySlots playerInventorySlots;

        private PlayerInventoryViewModelController _playerInventoryViewModelController;

        [Inject]
        public void Construct(PlayerInventoryViewModelController playerInventoryViewModelController)
        {
            _playerInventoryViewModelController = playerInventoryViewModelController;
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
            if (_playerInventoryViewModelController.IsGrabbed)
            {
                _playerInventoryViewModelController.CollectGrabbedItem();
            }
            else
            {
                _playerInventoryViewModelController.CollectSlotItem(slotIndex);
            }
        }

        private void CursorEnter(int slotIndex)
        {
            if (_playerInventoryViewModelController.IsItemSplitDragging)
            {
                //ドラッグ中の時はマウスカーソルが乗ったスロットをドラッグされたと判定する
                _playerInventoryViewModelController.ItemSplitDragSlot(slotIndex);
            }
            else if (_playerInventoryViewModelController.IsItemOneDragging)
            {
                _playerInventoryViewModelController.PlaceOneItem(slotIndex);
            }
        }

        private void RightClickUp(int slotIndex)
        {
            if (_playerInventoryViewModelController.IsItemOneDragging)
            {
                _playerInventoryViewModelController.ItemOneDragEnd();
            }
        }

        private void LeftClickUp(int slotIndex)
        {
            //左クリックを離したときはドラッグ中のスロットを解除する
            if (_playerInventoryViewModelController.IsItemSplitDragging)
            {
                _playerInventoryViewModelController.ItemSplitDragEndSlot(slotIndex);
            }
        }

        private void RightClickDown(int slotIndex)
        {
            if (_playerInventoryViewModelController.IsGrabbed)
            {
                //アイテムを持っている時に右クリックするとアイテム1個だけ置く処理
                _playerInventoryViewModelController.PlaceOneItem(slotIndex);
            }
            else
            {
                //アイテムを持ってない時に右クリックするとアイテムを半分とる処理
                _playerInventoryViewModelController.GrabbedHalfItem(slotIndex);
            }
            
        }
        
        private void LeftClickDown(int slotIndex)
        {
            if (_playerInventoryViewModelController.IsGrabbed)
            {
                //アイテムを持っている時に左クリックするとアイテムを置くもしくは置き換える処理
                _playerInventoryViewModelController.PlaceItem(slotIndex);
            }
            else
            {
                //アイテムを持ってない時に左クリックするとアイテムを取る処理
                _playerInventoryViewModelController.GrabbedItem(slotIndex);
            }
        }
        
        
    }
}