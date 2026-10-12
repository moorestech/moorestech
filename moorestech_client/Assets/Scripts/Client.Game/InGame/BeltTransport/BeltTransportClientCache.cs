using System;
using Game.Block.Blocks.BeltConveyor.Sync.Diff;
using Game.Block.Blocks.BeltConveyor.Sync.Replica;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using UniRx;

namespace Client.Game.InGame.BeltTransport
{
    // クライアントが持つベルト搬送の複製。全量で組み、毎tickの差分で進め、照合用のハッシュを出す
    // The client's replica of the belt transport: assembled from a full state, advanced by per-tick diffs, hashed for verification
    public sealed class BeltTransportClientCache
    {
        // 途中参加の全量が届くまでは無い。順序待ちの仕組みがその前のイベントを流さないので、差分が先に来ることは無い
        // Absent until the join-time full state arrives; the ordered buffer flushes nothing before it, so no diff can come first
        public BeltTransportReplica Replica { get; private set; }

        // 表示側が購読する。組み直しは全アイテムの作り直し、tickは位置の更新の合図
        // Subscribed by the view: a re-assembly cues rebuilding every item, a tick cues updating positions
        private readonly Subject<Unit> _onReplicaAssembled = new();
        private readonly Subject<Unit> _onReplicaTicked = new();
        public IObservable<Unit> OnReplicaAssembled => _onReplicaAssembled;
        public IObservable<Unit> OnReplicaTicked => _onReplicaTicked;

        public void ApplyFullState(BeltTransportFullState fullState)
        {
            Replica = BeltTransportReplicaAssembler.Assemble(fullState);
            _onReplicaAssembled.OnNext(Unit.Default);
        }

        // サーバーと同じtickを再生する。falseは複製がサーバーと食い違った印
        // Replays the same tick as the server; false marks the replica as diverged from the server
        public bool ApplyTickDiff(BeltTickDiff diff)
        {
            var consistent = Replica.Tick(diff);
            _onReplicaTicked.OnNext(Unit.Default);
            return consistent;
        }

        public uint ComputeCurrentHash()
        {
            return BeltTransportStateHash.Compute(Replica.CaptureFullState());
        }
    }
}
