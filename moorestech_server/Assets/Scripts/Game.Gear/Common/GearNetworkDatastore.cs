using System.Collections.Generic;
using Game.Block.Interface;
using Game.Gear.Tick;
using Game.Gear.Topology;

namespace Game.Gear.Common
{
    // 全gear networkの保持と、追加/削除コマンドの遅延適用・再計算対象の管理を担うデータストア
    // Datastore holding every gear network, applying add/remove commands lazily and tracking recalculation targets
    public class GearNetworkDatastore
    {
        private static GearNetworkDatastore _instance;

        private readonly GearNetworkTopologyMap _topologyMap;

        // 未適用のtopology変更コマンド。tick開始時または読み取り時にFIFOで一括適用される
        // Pending topology mutations, applied FIFO at tick start or on first read
        private readonly List<GearTopologyMutation> _pendingMutations = new();

        // 今tick再計算が必要なnetwork（topology変更またはgenerator出力変化）
        // Networks needing recalculation this tick (topology change or generator output change)
        private readonly HashSet<GearNetwork> _networksRequiringRecalc = new();

        // 毎tickの燃料更新が必要なgeneratorを含むnetwork。再計算対象とは別集合で管理する
        // Networks containing continuous-tick generators, tracked separately from the recalculation set
        private readonly HashSet<GearNetwork> _continuousTickNetworks = new();

        // 毎tickの過負荷破断チェック対象。GearTickUpdaterが全なめする（対象は過負荷設定を持つgearのみ）
        // Overload breakage targets swept every tick by GearTickUpdater (only gears with overload params register)
        private readonly HashSet<IGearOverloadTickTarget> _overloadTickTargets = new();

        private readonly GearRuntimeStateStore _runtimeStateStore;
        private bool _isFlushing;

        public GearNetworkDatastore()
        {
            _instance = this;
            _runtimeStateStore = new GearRuntimeStateStore();
            _topologyMap = new GearNetworkTopologyMap(MarkNetworkChanged, OnNetworkDiscarded);
        }

        public GearRuntimeStateStore RuntimeStateStore => _runtimeStateStore;
        public IReadOnlyCollection<GearNetwork> ContinuousTickNetworks => _continuousTickNetworks;

        public IReadOnlyDictionary<GearNetworkId, GearNetwork> GearNetworks
        {
            get
            {
                FlushPendingMutations();
                return _topologyMap.GearNetworks;
            }
        }

        public static void AddGear(IGearEnergyTransformer gear)
        {
            _instance._pendingMutations.Add(new GearTopologyMutation(GearTopologyMutationType.Add, gear));
        }

        public static void RemoveGear(IGearEnergyTransformer gear)
        {
            _instance._pendingMutations.Add(new GearTopologyMutation(GearTopologyMutationType.Remove, gear));
        }

        public static void RegisterOverloadTickTarget(IGearOverloadTickTarget target)
        {
            _instance._overloadTickTargets.Add(target);
        }

        public static void UnregisterOverloadTickTarget(IGearOverloadTickTarget target)
        {
            _instance._overloadTickTargets.Remove(target);
        }

        // 破断チェック対象をbufferへコピーする。sweep中の破断→登録解除で集合が変化しても安全に走査できるようにする
        // Copy overload targets into the buffer so breakage-triggered unregistration during the sweep cannot invalidate iteration
        public void CollectOverloadTickTargets(List<IGearOverloadTickTarget> buffer)
        {
            buffer.AddRange(_overloadTickTargets);
        }

        // generatorが出力変化時に自ら呼び、所属networkを次の再計算対象に加える
        // Called by a generator itself when its output changes, scheduling its network for recalculation
        public static void NotifyGeneratorOutputChanged(IGearEnergyTransformer generator)
        {
            if (_instance._topologyMap.TryGetNetwork(generator.BlockInstanceId, out var network))
                _instance._networksRequiringRecalc.Add(network);
        }

        // consumerが要求トルク変化時（稼働/idle切替等）に自ら呼び、所属networkを次の再計算対象に加える
        // Called by a consumer itself when its required torque changes (e.g. active/idle switch), scheduling its network for recalculation
        public static void NotifyRequiredTorqueChanged(IGearEnergyTransformer gear)
        {
            if (_instance._topologyMap.TryGetNetwork(gear.BlockInstanceId, out var network))
                _instance._networksRequiringRecalc.Add(network);
        }

        public static GearNetwork GetGearNetwork(BlockInstanceId blockInstanceId)
        {
            _instance.FlushPendingMutations();
            return _instance._topologyMap.GetNetwork(blockInstanceId);
        }

        // 未登録IDに対して例外を投げずに失敗を返す。プロトコル呼び出し側は存在しないブロックIDを送ってくる可能性があるため
        // Return a failure instead of throwing when the id is not registered; protocol callers may send ids for blocks that no longer exist
        public static bool TryGetGearNetwork(BlockInstanceId blockInstanceId, out GearNetwork network)
        {
            _instance.FlushPendingMutations();
            return _instance._topologyMap.TryGetNetwork(blockInstanceId, out network);
        }

        // 溜まったコマンドをFIFOで一括適用する。ロード中の読み取りでも正しいnetworkが見えるよう読み取り時にも呼ばれる
        // Apply pending mutations in FIFO order; also invoked on reads so loads that never tick still observe correct networks
        public void FlushPendingMutations()
        {
            if (_isFlushing || _pendingMutations.Count == 0) return;

            _isFlushing = true;
            for (var i = 0; i < _pendingMutations.Count; i++)
            {
                var mutation = _pendingMutations[i];
                if (mutation.MutationType == GearTopologyMutationType.Add) _topologyMap.AddGear(mutation.Gear);
                else RemoveGearInternal(mutation.Gear);
            }

            _pendingMutations.Clear();
            _isFlushing = false;
        }

        // 再計算対象networkをbufferへ移して内部集合をクリアする（安定tickでは空のまま）
        // Move networks requiring recalculation into the buffer and clear the internal set (stays empty on stable ticks)
        public void CollectNetworksRequiringRecalc(List<GearNetwork> buffer)
        {
            if (_networksRequiringRecalc.Count == 0) return;
            buffer.AddRange(_networksRequiringRecalc);
            _networksRequiringRecalc.Clear();
        }

        private void RemoveGearInternal(IGearEnergyTransformer gear)
        {
            // gear単位のruntime stateは保持していないため、topologyから外すだけでよい
            // No per-gear runtime state is kept, so removing it from the topology is sufficient
            _topologyMap.RemoveGear(gear);
        }

        // topology変化したnetworkのcacheを無効化し、再計算対象と燃料更新集合を更新する
        // Invalidate the changed network's cache and refresh the recalc / continuous-tick sets
        private void MarkNetworkChanged(GearNetwork network)
        {
            network.MarkTopologyDirty();
            _networksRequiringRecalc.Add(network);
            if (network.HasContinuousTickGenerator) _continuousTickNetworks.Add(network);
            else _continuousTickNetworks.Remove(network);
        }

        private void OnNetworkDiscarded(GearNetwork network)
        {
            _networksRequiringRecalc.Remove(network);
            _continuousTickNetworks.Remove(network);
            _runtimeStateStore.RemoveNetworkState(network.NetworkId);
        }
    }
}
