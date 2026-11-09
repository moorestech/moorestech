using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.ItemMove;
using Client.Ui.State.Items;
using Core.Item.Interface;
using UnityEngine;

namespace Client.Ui.State.SlotCommands.Block
{
    /// <summary>
    /// ブロックの枠の押下を操作の瞬間の在庫で計画し実行する（Web useBlockSlotGestures の写し）
    /// Plans and runs block-slot presses from the inventory at gesture time (port of Web useBlockSlotGestures)
    /// </summary>
    public sealed class BlockSlotCommands : IBlockSlotCommands
    {
        public const string CommandName = "block_inventory.slot";

        private readonly IBlockInventoryOperations _block;
        private readonly IPlayerInventoryOperations _player;
        private readonly PlannedItemActionRunner _runner;
        private readonly IItemStackLevelLookup _stackLevels;

        public BlockSlotCommands(IBlockInventoryOperations block, IPlayerInventoryOperations player, PlannedItemActionRunner runner, IItemStackLevelLookup stackLevels)
        {
            _block = block;
            _player = player;
            _runner = runner;
            _stackLevels = stackLevels;
        }

        public UiCommandResult LeftDown(int blockSlot, bool shift)
        {
            if (!TryReadSlot(blockSlot, out var slot)) return Unavailable(blockSlot);
            if (!KnownItemSlots.IsKnownOrEmpty(slot, nameof(BlockSlotCommands))) return UiCommandAccepted.Instance;
            var player = _player.ReadSnapshot();
            var plan = BlockSlotPlanner.PlanLeftClick(blockSlot, slot, shift, player.Grab.Count, player.MainSlots, _stackLevels);

            // Shift の配分が空（main が満杯）は Web と同じく何も送らない。開発者ログだけ残す（D-10）
            // An empty Shift allocation (main full) sends nothing like the Web; leave only a developer log (D-10)
            if (shift && player.Grab.Count == 0 && !slot.IsEmpty && plan.Count == 0) Debug.LogWarning($"[block_inventory.shift_move] not sent: destinationFull block slot={blockSlot} item={slot.ItemId}");
            return _runner.Execute(plan);
        }

        public UiCommandResult RightDown(int blockSlot)
        {
            if (!TryReadSlot(blockSlot, out var slot)) return Unavailable(blockSlot);
            return _runner.Execute(BlockSlotPlanner.PlanRightClick(blockSlot, slot, _player.ReadGrab().Count));
        }

        public UiCommandResult DoubleClick(int blockSlot)
        {
            if (!TryReadSlot(blockSlot, out _)) return Unavailable(blockSlot);
            return _runner.Execute(BlockSlotPlanner.PlanDoubleClick(blockSlot));
        }

        // 計画の入力は押下の瞬間に読む（描画時の写しを混ぜない）
        // Read planner inputs at gesture time (never mix render-time snapshots)
        private bool TryReadSlot(int blockSlot, out SlotItem slot)
        {
            slot = default;
            if (!_block.TryReadBlockSlots(out var slots) || !BlockSlotIndex.IsInRange(slots, blockSlot)) return false;
            slot = slots[blockSlot];
            return true;
        }

        private static UiCommandResult Unavailable(int blockSlot)
        {
            Debug.LogWarning($"[{CommandName}] stale press ignored: block slot {blockSlot} is not available");
            return new UiCommandRejected(UiCommandRejectReason.BlockSlotUnavailable, "");
        }
    }
}
