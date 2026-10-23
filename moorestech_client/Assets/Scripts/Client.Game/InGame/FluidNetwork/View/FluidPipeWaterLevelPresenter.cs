using System;
using Client.Game.InGame.Block;
using Game.Fluid.Sync.Replica;
using UnityEngine;

namespace Client.Game.InGame.FluidNetwork.View
{
    // 複製が進んだ回だけ、各ノードの充填率をそのパイプの水位表示へ押し込む。tick間の補間はしない
    // Whenever the replica has advanced, pushes each node's fill rate into that pipe's water-level view; no interpolation between ticks
    // 呼び出しは購読でなく、ベルトのアイテム表示と同じ場所(TrainUnitClientSimulator)から毎フレーム明示的に行う
    // Not driven by a subscription: TrainUnitClientSimulator calls this every frame in the same explicit order as the belt item display
    public sealed class FluidPipeWaterLevelPresenter
    {
        private readonly FluidNetworkClientCache _cache;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;

        // ノード番号→表示。複製の組み立てごとに作り直し、表示物がまだ無い番号は見つかるまで毎回引き直す
        // Node index → view; rebuilt per replica assembly, and an index whose object does not exist yet is looked up again until found
        private FluidNetworkReplica _boundReplica;
        private FluidPipeWaterLevelView[] _viewsByNode = Array.Empty<FluidPipeWaterLevelView>();
        private int _shownReplicaVersion;

        public FluidPipeWaterLevelPresenter(FluidNetworkClientCache cache, BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _cache = cache;
            _blockGameObjectDataStore = blockGameObjectDataStore;
        }

        // 複製の版が前回の表示から変わっていなければ何もしない
        // Does nothing while the replica version is unchanged since the last display
        public void UpdateAll()
        {
            var replica = _cache.Replica;
            if (replica == null || _cache.ReplicaVersion == _shownReplicaVersion) return;
            _shownReplicaVersion = _cache.ReplicaVersion;
            var network = replica.Network;
            if (!ReferenceEquals(_boundReplica, replica))
            {
                _boundReplica = replica;
                _viewsByNode = new FluidPipeWaterLevelView[network.NodeCount];
            }

            for (var i = 0; i < network.NodeCount; i++)
            {
                var view = ResolveView(i, network.NodePositions[i]);
                if (view == null) continue;
                view.SetWaterLevel((float)(network.NodeAmounts[i] / network.NodeCapacities[i]));
            }
        }

        // 撤去済みの表示はUnityのnull判定で外れ、同じ座標に置き直されたパイプは次の引き直しで新しい表示に繋がる
        // A destroyed view drops out through Unity's null check, and a pipe re-placed at the same position binds to its new view on the next lookup
        private FluidPipeWaterLevelView ResolveView(int nodeIndex, Vector3Int position)
        {
            var view = _viewsByNode[nodeIndex];
            if (view != null) return view;
            if (!_blockGameObjectDataStore.TryGetBlockGameObject(position, out var block)) return null;
            view = block.GetComponentInChildren<FluidPipeWaterLevelView>(true);
            _viewsByNode[nodeIndex] = view;
            return view;
        }
    }
}
