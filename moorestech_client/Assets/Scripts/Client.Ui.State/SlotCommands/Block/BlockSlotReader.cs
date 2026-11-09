using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;

namespace Client.Ui.State.SlotCommands.Block
{
    /// <summary>
    /// ブロックの枠を読む。開閉は表示と同じパネルの状態から（ホストと違う。D-37）、枠の値は押下の瞬間の生の値から（値の読み方はホストの BlockAreaSlotParser と同じ）
    /// Reads block slots: open/closed from the panel state the screen shows (unlike the host; D-37), values raw at press time (the value read matches the host BlockAreaSlotParser)
    /// </summary>
    public sealed class BlockSlotReader
    {
        private readonly IBlockInventoryPanelSource _panel;
        private readonly IRawSubInventorySlots _raw;

        public BlockSlotReader(IBlockInventoryPanelSource panel, IRawSubInventorySlots raw)
        {
            _panel = panel;
            _raw = raw;
        }

        public bool TryRead(out IReadOnlyList<SlotItem> slots)
        {
            if (!IsOpenWithoutError(_panel.Current.Value))
            {
                slots = Array.Empty<SlotItem>();
                return false;
            }
            slots = _raw.Read();
            return true;
        }

        private static bool IsOpenWithoutError(BlockInventoryPanelState state)
        {
            switch (state)
            {
                case OpenBlockInventoryPanel block:
                    return block.Error == BlockInventoryPanelError.None;
                case OpenTrainInventoryPanel train:
                    return train.Error == BlockInventoryPanelError.None;
                default:
                    return false;
            }
        }
    }
}
