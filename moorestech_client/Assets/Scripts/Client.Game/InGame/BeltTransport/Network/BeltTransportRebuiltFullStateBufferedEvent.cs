using Client.Game.InGame.Train.Network;
using Game.Block.Blocks.BeltConveyor.Sync.State;

namespace Client.Game.InGame.BeltTransport.Network
{
    // 列車の順序待ちキューに載る、組を作り直したtickの全量。流された時点で複製を全量から組み直す(そのtickの差分は再生しない)
    // The rebuilt tick's full state on the train's ordered buffer; re-assembles the replica from it when flushed (no diff is replayed for that tick)
    public sealed class BeltTransportRebuiltFullStateBufferedEvent : ITrainTickBufferedEvent
    {
        private readonly BeltTransportClientCache _cache;
        private readonly BeltTransportFullState _fullState;

        public BeltTransportRebuiltFullStateBufferedEvent(BeltTransportClientCache cache, BeltTransportFullState fullState)
        {
            _cache = cache;
            _fullState = fullState;
        }

        public void Apply()
        {
            _cache.ApplyFullState(_fullState);
        }
    }
}
