using System.Collections.Generic;

namespace Core.BeltTransport
{
    // 通常segment間の接続ごとに持つ、段階4の空き情報と確定した搬送
    // Per normal-to-normal connection: the stage-4 offer and the settled transfer
    internal sealed class BeltSegmentTransfer
    {
        private readonly BeltConveyorSegment _target;
        private readonly BeltDirection _inputDirection;
        private int _offer;
        private int _length;
        private BeltItem _item;

        internal BeltSegmentTransfer(BeltConveyorSegment target, BeltDirection inputDirection)
        {
            _target = target;
            _inputDirection = inputDirection;
        }

        // 通常segmentのうち、通常segmentへ搬出するものの接続一覧を作る
        // Build the connection list of normal segments that output into normal segments
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

        // 段階3完了後、通常segmentを1本も前進させる前に全接続分を記録する
        // After stage 3, record every connection before any normal segment advances
        internal void CaptureOffer()
        {
            _offer = _target.GetOffer(_inputDirection);
            _length = 0;
        }

        // この接続の搬出元だけが書く。受け入れ側にはまだ触らない
        // Written only by this connection's source. The receiver is not touched yet
        internal bool TryReceive(int entryLength, in BeltItem value)
        {
            if (entryLength > _offer) return false;
            _length = entryLength;
            _item = value;
            return true;
        }

        // 通常segmentは搬入元が最大1つ。更新後の末尾へ追加する
        // A normal segment has at most one input. Append after its update
        internal void Apply()
        {
            if (_length > 0) _target.ReceiveTransferred(_length, _item);
        }
    }
}
