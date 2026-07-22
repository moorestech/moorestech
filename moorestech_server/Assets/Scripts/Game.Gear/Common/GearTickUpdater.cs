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
        private readonly List<IGearOverloadTickTarget> _overloadBuffer = new();

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

            // 需給確定後に過負荷を判定する
            // Check overloads from settled values and isolate iteration from target-set mutations
            _overloadBuffer.Clear();
            _gearNetworkDatastore.CollectOverloadTickTargets(_overloadBuffer);
            foreach (var target in _overloadBuffer) target.TickOverloadCheck();

            // 継続駆動と状態通知を処理する
            // Update continuous generators only, then notify state changes for recalculated networks
            foreach (var network in _gearNetworkDatastore.ContinuousTickNetworks)
                network.ConsumeGeneratorTicks();
            foreach (var network in _recalcBuffer)
            {
                // 破壊はtick末尾の予約確定まで遅延するため通常成立しない防御ガード（破棄済みSubjectへの通知を防ぐ）
                // Defensive guard, normally unreachable since removals are reserved until tick end; prevents notifying disposed subjects
                foreach (var transformer in network.GearTransformers)
                    if (!transformer.IsDestroy) transformer.NotifyStateChanged();
                foreach (var generator in network.GearGenerators)
                    if (!generator.IsDestroy) generator.NotifyStateChanged();
            }
        }
    }
}
