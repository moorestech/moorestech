using System.Collections.Generic;
using Game.Gear.Tick;

namespace Game.Gear.Common
{
    // 適用済みgear網だけを更新する
    // Runs settlement, overload checks, and continuous generators for applied gear networks only
    public class GearTickUpdater
    {
        private readonly GearNetworkDatastore _gearNetworkDatastore;
        private readonly GearDemandSnapshotUpdater _demandSnapshotUpdater = new();
        private readonly List<GearNetwork> _recalcBuffer = new();

        public GearTickUpdater(GearNetworkDatastore gearNetworkDatastore)
        {
            _gearNetworkDatastore = gearNetworkDatastore;
        }

        public void Update()
        {
            // 再構築後のgear網だけを計算する
            // A preceding delegate owns topology rebuilding; calculate applied networks only here
            var demandSnapshot = _demandSnapshotUpdater.UpdateSnapshot();
            _recalcBuffer.Clear();
            _gearNetworkDatastore.CollectNetworksRequiringRecalc(_recalcBuffer);
            foreach (var network in _recalcBuffer) network.RunTick(demandSnapshot);

            // 需給確定後に過負荷を判定する。破断は予約されtick末尾まで集合は変わらない
            // Check overloads from settled values; breakage is reserved, so the target set does not change until tick end
            foreach (var target in _gearNetworkDatastore.OverloadTickTargets) target.TickOverloadCheck();

            // 継続駆動と状態通知を処理する
            // Update continuous generators only, then notify state changes for recalculated networks
            foreach (var network in _gearNetworkDatastore.ContinuousTickNetworks)
                network.ConsumeGeneratorTicks();
            foreach (var network in _recalcBuffer)
            {
                foreach (var transformer in network.GearTransformers) transformer.NotifyStateChanged();
                foreach (var generator in network.GearGenerators) generator.NotifyStateChanged();
            }
        }
    }
}
