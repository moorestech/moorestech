using Client.Game.InGame.Train.Network;
using Game.Fluid.Sync.State;

namespace Client.Game.InGame.FluidNetwork.Network
{
    // 列車の順序待ちキューに載る、網を作り直したtickの全量。流された時点で複製を全量から組み直す(そのtickの差分は先に再生済み)
    // The rebuilt tick's full state on the train's ordered buffer; re-assembles the replica from it when flushed (that tick's diff was already replayed)
    public sealed class FluidNetworkRebuiltFullStateBufferedEvent : ITrainTickBufferedEvent
    {
        private readonly FluidNetworkClientCache _cache;
        private readonly FluidNetworkFullState _fullState;

        public FluidNetworkRebuiltFullStateBufferedEvent(FluidNetworkClientCache cache, FluidNetworkFullState fullState)
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
