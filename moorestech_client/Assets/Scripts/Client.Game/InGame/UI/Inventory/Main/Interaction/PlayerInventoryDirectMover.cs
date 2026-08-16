// [uGUI廃止Phase1] Web UI移行済みのため未メンテ・描画恒久停止。Phase2で削除予定（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] Unmaintained; rendering permanently disabled after the Web UI migration. Slated for deletion in Phase2 (docs/webui/ugui-retirement-plan.md)
using Core.Master;
using Core.Item.Interface;
using Core.Item;

namespace Client.Game.InGame.UI.Inventory.Main
{
    /// <summary>
    /// Shift+クリックで直接移動(メイン/サブ間)を担う
    /// Handles Shift+click direct item moves across main/sub inventories
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

                return InventoryType.MainInventory;
            }

            (int start, int end) GetTargetRange(InventoryType source, bool hasSub)
            {
                var mainSlotCount = _playerInventory.LocalPlayerInventory.MainSlotCount;
                switch (source)
                {
                    case InventoryType.MainInventory:
                        // メインインベントリから：サブがあればサブへ、なければ配分先なし（旧ホットバー振り分けは廃止済み）
                        return hasSub ? (mainSlotCount, mainSlotCount + subInventory.Count) : (0, 0);

                    case InventoryType.SubInventory:
                        // サブから：メイン全域へ
                        return (0, mainSlotCount);

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
            SubInventory
        }
    }
}
