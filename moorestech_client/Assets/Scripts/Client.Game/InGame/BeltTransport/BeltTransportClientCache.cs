using Game.Block.Blocks.BeltConveyor.Sync.Diff;
using Game.Block.Blocks.BeltConveyor.Sync.Replica;
using Game.Block.Blocks.BeltConveyor.Sync.State;

namespace Client.Game.InGame.BeltTransport
{
    // クライアントが持つベルト搬送の複製。全量で組み、毎tickの差分で進め、照合用のハッシュを出す
    // The client's replica of the belt transport: assembled from a full state, advanced by per-tick diffs, hashed for verification
    public sealed class BeltTransportClientCache
    {
        // 途中参加の全量が届くまでは無い。順序待ちの仕組みがその前のイベントを流さないので、差分が先に来ることは無い
        // Absent until the join-time full state arrives; the ordered buffer flushes nothing before it, so no diff can come first
        public BeltTransportReplica Replica { get; private set; }

        // 組み直しとtickのたびに進む版。表示側は購読でなく、描画更新の順番の中でこの版を見て作り直す
        // Advances on every re-assembly and tick; the view compares this inside its explicit update order instead of subscribing
        public int ReplicaVersion { get; private set; }

        public void ApplyFullState(BeltTransportFullState fullState)
        {
            Replica = BeltTransportReplicaAssembler.Assemble(fullState);
            ReplicaVersion++;
        }

        // サーバーと同じtickを再生する。falseは複製がサーバーと食い違った印
        // Replays the same tick as the server; false marks the replica as diverged from the server
        public bool ApplyTickDiff(BeltTickDiff diff)
        {
            var consistent = Replica.Tick(diff);
            ReplicaVersion++;
            return consistent;
        }

        public uint ComputeCurrentHash()
        {
            return BeltTransportStateHash.Compute(Replica.CaptureFullState());
        }
    }
}
