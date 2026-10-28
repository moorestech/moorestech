using Game.Fluid.Sync.Diff;
using Game.Fluid.Sync.Replica;
using Game.Fluid.Sync.State;
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

        // 組み直しとtickのたびに進む版。表示側は購読でなく、描画更新の順番の中でこの版を見て作り直す
        // Advances on every re-assembly and tick; the view compares this inside its explicit update order instead of subscribing
        public int ReplicaVersion { get; private set; }

        public void ApplyFullState(FluidNetworkFullState fullState)
        {
            Replica = FluidNetworkReplicaAssembler.Assemble(fullState);
            ReplicaVersion++;
        }

        // サーバーと同じtickを再生する。falseは複製がサーバーと食い違った印
        // Replays the same tick as the server; false marks the replica as diverged from the server
        public bool ApplyTickDiff(FluidTickDiff diff)
        {
            var consistent = Replica.Tick(diff);
            ReplicaVersion++;
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
