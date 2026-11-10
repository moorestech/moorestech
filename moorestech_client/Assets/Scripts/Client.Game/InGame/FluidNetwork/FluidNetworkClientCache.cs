using System;
using Game.Fluid.Sync.Diff;
using Game.Fluid.Sync.Replica;
using Game.Fluid.Sync.State;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.FluidNetwork
{
    // クライアントが持つ流体網の複製。全量で組み、毎tickの差分で進め、照合用のハッシュを出す
    // The client's replica of the fluid network: assembled from a full state, advanced by per-tick diffs, hashed for verification
    public sealed class FluidNetworkClientCache
    {
        // 途中参加の全量が届くまでは無い。順序待ちの仕組みがその前のイベントを流さないので、差分が先に来ることは無い
        // Absent until the join-time full state arrives; the ordered buffer flushes nothing before it, so no diff can come first
        public FluidNetworkReplica Replica { get; private set; }

        // 組み直しとtickのたびに進む版。パイプの水位の表示は描画更新の順番の中で版を見る。UI は OnReplicaVersionChanged を購読する
        // Advances on every re-assembly and tick; the pipe water level compares it inside the explicit update order, while the UI subscribes to OnReplicaVersionChanged
        public int ReplicaVersion { get; private set; }

        // 複製と版を進め終えた後に版を流す。購読者の例外は呼び手へ同期で戻る（前例 TrainUnitClientCache.OnUnitApplied と同じ）
        // Emits the version after the replica and version have advanced; a subscriber's exception returns synchronously to the caller (same as the precedent TrainUnitClientCache.OnUnitApplied)
        public IObservable<int> OnReplicaVersionChanged => _replicaVersionChanged;
        private readonly Subject<int> _replicaVersionChanged = new();

        public void ApplyFullState(FluidNetworkFullState fullState)
        {
            Replica = FluidNetworkReplicaAssembler.Assemble(fullState);
            ReplicaVersion++;
            _replicaVersionChanged.OnNext(ReplicaVersion);
        }

        // サーバーと同じtickを再生する。falseは複製がサーバーと食い違った印
        // Replays the same tick as the server; false marks the replica as diverged from the server
        public bool ApplyTickDiff(FluidTickDiff diff)
        {
            var consistent = Replica.Tick(diff);
            ReplicaVersion++;
            // 食い違い（false）でも複製は進んでいるので流す。食い違いの扱いは呼び手のまま
            // Emit even when diverged (false) since the replica did advance; the caller still owns the divergence handling
            _replicaVersionChanged.OnNext(ReplicaVersion);
            return consistent;
        }

        // 直前の tick(または組み立て)時点のハッシュ。Stepper が走査のついでに足している
        // Hash as of the last tick (or assembly), folded in by the stepper during its own walk
        public uint CurrentHash => Replica.Network.LastStateHash;

        // パイプの座標からノード番号を引く。複製が無い間、パイプでない座標はfalse
        // Looks up the node index at a pipe position; false without a replica or off a pipe
        public bool TryGetNodeIndex(Vector3Int position, out int nodeIndex)
        {
            if (Replica != null) return Replica.TryGetNodeIndex(position, out nodeIndex);
            nodeIndex = -1;
            return false;
        }
    }
}
