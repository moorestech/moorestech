using Core.BeltTransport;

namespace Game.Block.Blocks.BeltConveyor.Sync.Replica
{
    // 複製での機械。受け入れの成否はサーバーが確定した差分で与えるので、差分で許可されたtickだけ1個受け入れ、それ以外は出口で待たせる
    // A machine in the replica; acceptance is dictated by the server's settled diffs, so it takes one item only on a tick the diff allowed and otherwise keeps the item waiting at the exit
    public sealed class BeltReplicaMachineReceiver : IBeltReceiver
    {
        private bool _acceptsOnce;

        // 機械は搬入元を問い合わせないので登録しない
        // A machine never queries its sources, so nothing is registered
        public void AttachInput(IBeltSource source, BeltDirection inputDirection)
        {
        }

        // サーバーの機械と同じく、空きは常に1マス分と答える
        // Like the server's machine, the offer is always one full cell
        public int GetOffer(BeltDirection inputDirection)
        {
            return BeltConstants.ItemWidth;
        }

        // 差分の再生がこのtickの搬出成功を予告する。次のTryReceiveで1回だけ受け入れる
        // Diff replay announces this tick's successful handoff; the next TryReceive accepts exactly once
        public void AcceptOnce()
        {
            _acceptsOnce = true;
        }

        public bool TryReceive(BeltDirection inputDirection, int length, in BeltItem item)
        {
            if (!_acceptsOnce) return false;
            _acceptsOnce = false;
            return true;
        }
    }
}
