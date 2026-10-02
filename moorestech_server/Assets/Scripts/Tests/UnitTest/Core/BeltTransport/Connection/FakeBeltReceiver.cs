using System.Collections.Generic;
using Core.BeltTransport;

namespace Tests.UnitTest.Core.BeltTransport.Connection
{
    // 空き距離と受け入れ可否を外から決められる搬入先。呼び出しを全て記録する
    // A receiver whose offer and acceptance are set from outside. Records every call
    public sealed class FakeBeltReceiver : IBeltReceiver
    {
        public readonly List<(IBeltSource Source, BeltDirection Direction)> AttachedInputs = new();
        public readonly List<(BeltDirection Direction, int Length, BeltItem Item)> ReceiveAttempts = new();
        public readonly List<BeltItem> ReceivedItems = new();
        private int _offer;
        private bool _accepts;

        public FakeBeltReceiver(int offer, bool accepts)
        {
            _offer = offer;
            _accepts = accepts;
        }

        public void SetOffer(int offer)
        {
            _offer = offer;
        }

        public void SetAccepts(bool accepts)
        {
            _accepts = accepts;
        }

        public void AttachInput(IBeltSource source, BeltDirection inputDirection)
        {
            AttachedInputs.Add((source, inputDirection));
        }

        public int GetOffer(BeltDirection inputDirection)
        {
            return _offer;
        }

        public bool TryReceive(BeltDirection inputDirection, int length, in BeltItem item)
        {
            ReceiveAttempts.Add((inputDirection, length, item));
            if (!_accepts) return false;
            ReceivedItems.Add(item);
            return true;
        }
    }
}
