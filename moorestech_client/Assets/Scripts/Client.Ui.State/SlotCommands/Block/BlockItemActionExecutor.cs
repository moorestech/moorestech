using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using UnityEngine;

namespace Client.Ui.State.SlotCommands.Block
{
    /// <summary>
    /// ブロック側の行為を、開いているか・枠が範囲内かを確かめてから実行する
    /// Runs a block-side action after checking the panel is open and the slots are in range
    /// </summary>
    public sealed class BlockItemActionExecutor
    {
        private readonly IBlockInventoryOperations _block;
        private readonly IPlayerInventoryOperations _player;

        public BlockItemActionExecutor(IBlockInventoryOperations block, IPlayerInventoryOperations player)
        {
            _block = block;
            _player = player;
        }

        public UiCommandResult Execute(BlockItemAction action)
        {
            // 閉じた・枠が減った後の古い計画は BlockSlotUnavailable で拒否（ホストの invalid_slot。Web と同じくトーストする）
            // A stale plan after closing or shrinking is rejected as BlockSlotUnavailable (the host's invalid_slot; toasted like the Web)
            if (!_block.TryReadBlockSlots(out var slots)) return Unavailable("block inventory is closed or in error");
            if (!action.IsInRange(slots)) return Unavailable($"block slot out of range: {action}");
            return action.Run(_block, slots, _player.ReadGrab().IsEmpty);

            #region Internal

            UiCommandResult Unavailable(string detail)
            {
                // 説明文はログだけへ。wire は理由の既定コードのまま（ToWireCode が "invalid_slot" を返す）
                // The description goes to the log only; the wire stays the reason's code (ToWireCode returns "invalid_slot")
                Debug.LogWarning($"[BlockItemActionExecutor] {detail}");
                return UiCommandRejected.Of(UiCommandRejectReason.BlockSlotUnavailable);
            }

            #endregion
        }
    }
}
