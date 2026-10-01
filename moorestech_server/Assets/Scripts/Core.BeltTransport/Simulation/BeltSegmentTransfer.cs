// MyBeltConvSegmentのCPU実装をムアステ向けに変更。
// Adapted from MyBeltConvSegment CPU implementation.
using System.Collections.Generic;

namespace Core.BeltTransport
{
    /// <summary>通常segment間の接続ごとに持つ、段階4の空き情報と確定した搬送。</summary>
    internal sealed class BeltSegmentTransfer
    {
        readonly BeltConveyorSegment target;
        readonly BeltDirection inputDirection;
        int offer, length;
        BeltItem item;

        internal BeltSegmentTransfer(BeltConveyorSegment target, BeltDirection inputDirection)
        {
            this.target = target;
            this.inputDirection = inputDirection;
        }

        internal static BeltSegmentTransfer[] Cache(IEnumerable<BeltConveyorSegment> normal)
        {
            var transfers = new List<BeltSegmentTransfer>();
            foreach (var segment in normal)
            {
                var transfer = segment.CacheTransfer();
                if (transfer != null) transfers.Add(transfer);
            }
            return transfers.ToArray();
        }

        // 通常segmentの前進前に全接続の空きを記録する。
        // Capture every connection before any normal segment advances.
        internal void CaptureOffer()
        {
            offer = target.GetOffer(inputDirection);
            length = 0;
        }

        // 搬出元が確定し、搬入先への反映は待つ。
        // The source commits while target mutation remains deferred.
        internal bool TryReceive(int entryLength, in BeltItem value)
        {
            if (offer < entryLength) return false;
            length = entryLength;
            item = value;
            return true;
        }

        // 更新後の末尾へ確定した搬入を反映する。
        // Apply committed input after target advancement.
        internal void Apply()
        {
            if (0 < length) target.ReceiveTransferred(length, item);
        }
    }
}
