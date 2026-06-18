using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.Connector;
using Game.Context;

namespace Game.Block.Blocks.BeltConveyor
{
    internal class VanillaBeltConveyorUpdateService
    {
        private readonly VanillaBeltConveyorInventoryItem[] _inventoryItems;
        private readonly IBeltConveyorBlockInventoryInserter _blockInventoryInserter;

        public VanillaBeltConveyorUpdateService(
            VanillaBeltConveyorInventoryItem[] inventoryItems,
            IBeltConveyorBlockInventoryInserter blockInventoryInserter)
        {
            _inventoryItems = inventoryItems;
            _blockInventoryInserter = blockInventoryInserter;
        }

        public void Update(uint ticksOfItemEnterToExit, int inventoryItemNum)
        {
            var count = _inventoryItems.Length;
            var ticksPerSlot = ticksOfItemEnterToExit / (uint)inventoryItemNum;

            for (var i = 0; i < count; i++)
            {
                var item = _inventoryItems[i];
                if (item == null) continue;

                // 目標コネクタが消えていた場合だけ再選択する
                // Re-select the goal connector only when the current one vanished.
                ValidateAndUpdateGoalConnector(item);

                var nextIndexStartTicks = (uint)i * ticksPerSlot;
                var isNextInsertable = item.RemainingTicks <= nextIndexStartTicks;
                var didMove = TryMoveToNextSlot(i, item, isNextInsertable);

                if (TryOutputItem(i, item)) continue;

                // 前スロット詰まり時は進行 tick を減らさない
                // Do not decrement progress while blocked by the previous slot.
                var isBlockedByPreviousSlot = !didMove && i != 0 && _inventoryItems[i - 1] != null && isNextInsertable;
                if (!isBlockedByPreviousSlot && item.RemainingTicks > 0) item.RemainingTicks--;
            }
        }

        private bool TryMoveToNextSlot(int slot, VanillaBeltConveyorInventoryItem item, bool isNextInsertable)
        {
            if (!isNextInsertable || slot == 0) return false;
            if (_inventoryItems[slot - 1] != null) return false;

            _inventoryItems[slot - 1] = item;
            _inventoryItems[slot] = null;
            return true;
        }

        private bool TryOutputItem(int slot, VanillaBeltConveyorInventoryItem item)
        {
            if (slot != 0 || item.RemainingTicks != 0) return false;

            var insertItem = ServerContext.ItemStackFactory.Create(item.ItemId, 1, item.ItemInstanceId);
            var output = _blockInventoryInserter.InsertItem(insertItem, item.GoalConnector);
            if (output.Id == ItemMaster.EmptyItemId) _inventoryItems[slot] = null;
            return true;
        }

        private void ValidateAndUpdateGoalConnector(VanillaBeltConveyorInventoryItem targetItem)
        {
            if (_blockInventoryInserter.ConnectedCount == 0) return;
            if (_blockInventoryInserter.IsValidGoalConnector(targetItem.GoalConnector)) return;

            var checkItems = new List<Core.Item.Interface.IItemStack>
            {
                ServerContext.ItemStackFactory.Create(targetItem.ItemId, 1, targetItem.ItemInstanceId)
            };
            var goalConnector = _blockInventoryInserter.PeekNextGoalConnector(checkItems);
            if (goalConnector == null) return;
            targetItem.SetGoalConnector(goalConnector);
        }
    }
}
