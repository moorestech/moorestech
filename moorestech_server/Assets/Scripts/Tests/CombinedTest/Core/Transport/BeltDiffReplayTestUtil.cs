using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.BeltConveyor.Sync.Diff;
using Game.Block.Blocks.BeltConveyor.Sync.Replica;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using NUnit.Framework;

namespace Tests.CombinedTest.Core.Transport
{
    // 実tickで進むサーバーの搬送と、全量＋差分だけで進む複製を並べて比べる補助
    // Helpers that run the server transport through real ticks next to a replica driven only by the full state and diffs
    internal static class BeltDiffReplayTestUtil
    {
        internal static BeltTransportDatastore Datastore()
        {
            return ServerContext.GetService<BeltTransportDatastore>();
        }

        // 未取り出しの差分を捨て、現在の組の全量から複製を組む。クライアントの初回同期に当たる
        // Drop untaken diffs and assemble a replica from the current assembly's full state; the client's initial sync
        internal static BeltTransportReplica StartReplica()
        {
            Datastore().DiffRecorder.TakeTickDiff();
            return BeltTransportReplicaAssembler.Assemble(BeltTransportFullStateCapture.Capture(Datastore().Assembly));
        }

        // 実tickを1回進め、そのtickの差分を取り出す
        // Advance one real tick and take that tick's diff
        internal static BeltTickDiff TickServer()
        {
            GameUpdater.UpdateOneTick();
            return Datastore().DiffRecorder.TakeTickDiff();
        }

        // 差分を複製で再生し、食い違いが無くハッシュがサーバーと一致することを確かめる
        // Replay the diff on the replica and check it stays consistent and its hash matches the server
        internal static void ReplayAndAssert(BeltTransportReplica replica, BeltTickDiff diff, int tick)
        {
            Assert.IsTrue(replica.Tick(diff), $"replica consistent at tick {tick}");
            Assert.AreEqual(ServerHash(), BeltTransportStateHash.Compute(replica.CaptureFullState()), $"hash at tick {tick}");
        }

        internal static uint ServerHash()
        {
            return BeltTransportStateHash.Compute(BeltTransportFullStateCapture.Capture(Datastore().Assembly));
        }

        // 機械(チェスト)からの押し込みと同じ文脈で、ベルコンの搬入口へ1個押し込む。入ったらtrue
        // Push one item into the belt inlet with the same context a machine (chest) push carries; true when it entered
        internal static bool PushThroughInlet(IBlockInventory inlet, IBlock machine, IItemStack stack)
        {
            var context = new InsertItemContext(machine.BlockInstanceId, null, null);
            return inlet.InsertItem(stack, context).Count == stack.Count - 1;
        }
    }
}
