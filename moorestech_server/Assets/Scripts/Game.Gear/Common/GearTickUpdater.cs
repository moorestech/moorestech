using System.Collections.Generic;
using Game.Gear.Tick;

namespace Game.Gear.Common
{
    // gear関連tickの唯一の入口。GameUpdater.AdditionalUpdatesに登録され、BlockSystem.Updateより前に走る
    // Sole entry point of gear ticks, registered in GameUpdater.AdditionalUpdates to run before BlockSystem.Update
    public class GearTickUpdater
    {
        // 直近tickで需給再計算したnetwork数。安定tickで0になることの診断に使う
        // Networks recalculated in the last tick; used to verify stable ticks process zero networks
        public int LastTickRecalculatedNetworkCount { get; private set; }

        // 直近tickでtraversal cacheを再構築したnetwork数
        // Networks whose traversal cache was rebuilt in the last tick
        public int LastTickTraversalRebuildCount { get; private set; }

        private readonly GearNetworkDatastore _gearNetworkDatastore;
        private readonly GearDemandSnapshotUpdater _demandSnapshotUpdater = new();

        // 再計算対象を毎tick使い回すバッファ。安定tickではアロケーションが発生しない
        // Buffer reused every tick for recalculation targets; stable ticks allocate nothing
        private readonly List<GearNetwork> _recalcBuffer = new();

        public GearTickUpdater(GearNetworkDatastore gearNetworkDatastore)
        {
            _gearNetworkDatastore = gearNetworkDatastore;
        }

        public void Update()
        {
            // 1) 溜まったgear追加/削除コマンドをFIFOで一括適用し、tick内topology不変を確定する
            // 1) Apply pending gear add/remove commands FIFO, fixing the topology for the rest of the tick
            _gearNetworkDatastore.FlushPendingMutations();

            // 2) そのtickの需要snapshotを作成する
            // 2) Build the demand snapshot for this tick
            var demandSnapshot = _demandSnapshotUpdater.UpdateSnapshot();

            // 3-5) 再計算が必要なnetworkのみ列挙し、需給計算とstore書き込みを行う
            // 3-5) Enumerate only networks needing recalculation, then calculate supply-demand and write the store
            var store = _gearNetworkDatastore.RuntimeStateStore;
            _recalcBuffer.Clear();
            _gearNetworkDatastore.CollectNetworksRequiringRecalc(_recalcBuffer);
            LastTickRecalculatedNetworkCount = _recalcBuffer.Count;
            LastTickTraversalRebuildCount = 0;
            foreach (var network in _recalcBuffer)
            {
                var rebuilt = network.RunTick(demandSnapshot, store);
                if (rebuilt) LastTickTraversalRebuildCount++;
            }

            // 6) 毎tick駆動が必要なgeneratorを含むnetworkだけ燃料消費と出力更新を行う
            // 6) Consume fuel and update output only for networks containing continuous-tick generators
            foreach (var network in _gearNetworkDatastore.ContinuousTickNetworks)
            {
                network.ConsumeGeneratorTicks(store);
            }

            // 7) 再計算したnetworkのgearだけ、tick最後にまとめてクライアントへ状態変化を通知する（安定tickは通知ゼロ）
            // 7) Notify clients at tick end only for gears in recalculated networks (stable ticks emit nothing)
            foreach (var network in _recalcBuffer)
            {
                foreach (var transformer in network.GearTransformers) transformer.NotifyStateChanged();
                foreach (var generator in network.GearGenerators) generator.NotifyStateChanged();
            }
        }
    }
}
