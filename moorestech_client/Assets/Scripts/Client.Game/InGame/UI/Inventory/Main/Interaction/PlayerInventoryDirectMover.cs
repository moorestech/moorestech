using Core.Master;
using Game.PlayerInventory.Interface;
using Core.Item.Interface;
using Core.Item;

namespace Client.Game.InGame.UI.Inventory.Main
{
    /// <summary>
    /// Shift+クリックのアイテム直接移動（メイン/ホットバー/サブ間）を担う
    /// Handles Shift+click direct item moves across main/hotbar/sub inventories
    /// </summary>
    public class PlayerInventoryDirectMover
    {
        private readonly LocalPlayerInventoryController _playerInventory;

        public PlayerInventoryDirectMover(LocalPlayerInventoryController playerInventory)
        {
            _playerInventory = playerInventory;
        }

        public void Move(int slotIndex, ISubInventory subInventory)
        {
            // 移動するアイテムが空の場合は何もしない
            var sourceItem = _playerInventory.LocalPlayerInventory[slotIndex];
            if (sourceItem.Id == ItemMaster.EmptyItemId) return;

            // サブインベントリの有無を判定
            var hasSubInventory = subInventory != null && subInventory.IsEnableSubInventory();

            // 移動元の種類を判定
            var sourceType = GetInventoryType(slotIndex, hasSubInventory);

            // 移動先の範囲を決定
            var (startIndex, endIndex) = GetTargetRange(sourceType, hasSubInventory);

            // 移動先を探して移動
            TryMoveToSlots(slotIndex, sourceItem, startIndex, endIndex);

            #region Internal

            InventoryType GetInventoryType(int index, bool hasSub)
            {
                var mainSlotCount = _playerInventory.LocalPlayerInventory.MainSlotCount;
                if (hasSub && index >= mainSlotCount)
                    return InventoryType.SubInventory;

                // ホットバーの判定
                if (_playerInventory.LocalPlayerInventory.IsHotBarSlot(index))
                    return InventoryType.HotBar;

                return InventoryType.MainInventory;
            }

            (int start, int end) GetTargetRange(InventoryType source, bool hasSub)
            {
                var mainSlotCount = _playerInventory.LocalPlayerInventory.MainSlotCount;
                var hotBarStart = PlayerInventoryConst.HotBarSlotToInventorySlot(0, mainSlotCount);
                switch (source)
                {
                    case InventoryType.MainInventory:
                        // メインインベントリから：サブがあればサブへ、なければホットバーへ
                        return hasSub ? (mainSlotCount, mainSlotCount + subInventory.Count) : (hotBarStart, mainSlotCount);

                    case InventoryType.HotBar:
                        // ホットバーから：サブがあればサブへ、なければメインインベントリへ
                        return hasSub ? (mainSlotCount, mainSlotCount + subInventory.Count) : (0, hotBarStart);

                    case InventoryType.SubInventory:
                        // サブインベントリから：メインインベントリへ（ホットバーを除く）
                        return (0, hotBarStart);

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

                var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(targetItem.Id);
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
    }
}
