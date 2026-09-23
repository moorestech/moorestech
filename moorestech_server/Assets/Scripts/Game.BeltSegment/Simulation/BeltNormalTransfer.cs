using System;

namespace Game.BeltSegment
{
    internal sealed class BeltNormalTransfer
    {
        readonly BeltConveyorSegment target;
        readonly BeltDirection inputDirection;
        int availableSpace;
        int stagedLength;
        BeltItem stagedItem;
        bool hasStagedItem;

        internal BeltNormalTransfer(BeltConveyorSegment target, BeltDirection inputDirection)
        {
            this.target = target;
            this.inputDirection = inputDirection;
        }

        internal void CaptureAvailableSpace()
        {
            // 搬入判定は全segmentの前進前の実空きに固定する。
            // Freeze actual entrance space before any segment advances.
            availableSpace = target.GetOffer(inputDirection);
        }

        internal bool TryStage(int length, in BeltItem item)
        {
            if (availableSpace < length) return false;
            stagedItem = item;
            stagedLength = length;
            hasStagedItem = true;
            return true;
        }

        internal void Commit()
        {
            if (!hasStagedItem) return;
            // 全Normal前進後に一度だけ入力を確定する。
            // Commit incoming items once after all Normal advances.
            if (!target.TryReceive(inputDirection, stagedLength, stagedItem))
                throw new InvalidOperationException("A staged Normal transfer lost its reserved entrance space.");
            hasStagedItem = false;
            stagedItem = default;
            stagedLength = 0;
        }
    }
}
