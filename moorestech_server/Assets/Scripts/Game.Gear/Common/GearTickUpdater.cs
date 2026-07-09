using System.Collections.Generic;
using Game.Gear.Tick;

namespace Game.Gear.Common
{
    // gear関連tickの唯一の入口。GameUpdater.AdditionalUpdatesに登録され、BlockSystem.Updateより前に走る
    // Sole entry point of gear ticks, registered in GameUpdater.AdditionalUpdates to run before BlockSystem.Update
    public class GearTickUpdater
    {
        private readonly GearNetworkDatastore _gearNetworkDatastore;
        private readonly GearDemandSnapshotUpdater _demandSnapshotUpdater = new();

        // 再計算対象を毎tick使い回すバッファ。安定tickではアロケーションが発生しない
        // Buffer reused every tick for recalculation targets; stable ticks allocate nothing
        private readonly List<GearNetwork> _recalcBuffer = new();

        // 破断チェック対象を毎tick使い回すバッファ。sweep中の破断による登録解除から走査を守る
        // Buffer reused every tick for overload targets, shielding the sweep from breakage-triggered unregistration
        private readonly List<IGearOverloadTickTarget> _overloadBuffer = new();

        public GearTickUpdater(GearNetworkDatastore gearNetworkDatastore)
        {
            _gearNetworkDatastore = gearNetworkDatastore;
        }

        public void Update()
        {
            // 溜まったgear追加/削除コマンドをFIFOで一括適用し、tick内topology不変を確定する
            // Apply pending gear add/remove commands FIFO, fixing the topology for the rest of the tick
            _gearNetworkDatastore.FlushPendingMutations();

            // そのtickの需要snapshotを作成する
            // Build the demand snapshot for this tick
            var demandSnapshot = _demandSnapshotUpdater.UpdateSnapshot();

            // 再計算が必要なnetworkのみ列挙し、需給計算とstore書き込みを行う
            // Enumerate only networks needing recalculation, then calculate supply-demand and write the store
            // Future仕様注意: consumer需要の変化だけではnetworkは再計算されない。動的需要導入時は需要変化通知も追加する
            // Future-spec note: consumer-demand changes alone do not recalculate networks; add demand-change notification with dynamic demand
            var store = _gearNetworkDatastore.RuntimeStateStore;
            _recalcBuffer.Clear();
            _gearNetworkDatastore.CollectNetworksRequiringRecalc(_recalcBuffer);
            foreach (var network in _recalcBuffer)
            {
                network.RunTick(demandSnapshot, store);
            }

            // 過負荷破断チェックは、需給確定後の導出値で対象を走査する
            // Sweep overload breakage targets using post-settlement derived values
            _overloadBuffer.Clear();
            _gearNetworkDatastore.CollectOverloadTickTargets(_overloadBuffer);
            foreach (var target in _overloadBuffer) target.TickOverloadCheck();

            // 毎tick駆動が必要なgeneratorを含むnetworkだけ燃料消費と出力更新を行う
            // Consume fuel and update output only for networks containing continuous-tick generators
            foreach (var network in _gearNetworkDatastore.ContinuousTickNetworks)
            {
                network.ConsumeGeneratorTicks(store);
            }

            // 再計算したnetworkのgearだけ、tick最後にまとめてクライアントへ状態変化を通知する
            // Notify clients at tick end only for gears in recalculated networks
            foreach (var network in _recalcBuffer)
            {
                // 同tickの破断sweepで破壊されたgearは通知Subjectがdispose済みのためスキップする
                // Skip gears destroyed by this tick's breakage sweep; their notification subjects are already disposed
                foreach (var transformer in network.GearTransformers)
                    if (!transformer.IsDestroy) transformer.NotifyStateChanged();
                foreach (var generator in network.GearGenerators)
                    if (!generator.IsDestroy) generator.NotifyStateChanged();
            }
        }
    }
}
