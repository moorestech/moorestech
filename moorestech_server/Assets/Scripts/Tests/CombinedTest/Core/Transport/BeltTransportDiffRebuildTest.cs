using System.Linq;
using Core.Item;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Sync.Replica;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using Game.Block.Interface;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.UnitTest.Game.BeltConnection.Sync;
using UnityEngine;
using static Tests.CombinedTest.Core.Transport.BeltDiffReplayTestUtil;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.CombinedTest.Core.Transport
{
    // 設置・撤去で組が作り直されたtickは、複製をそのtick後の全量から組み直して差分の再生を続ければサーバーと一致し続けるか
    // Whether, on a tick whose head rebuilt the assembly after a placement or removal, re-assembling the replica from that tick's post-tick full state and resuming diff replay keeps it identical to the server
    public class BeltTransportDiffRebuildTest
    {
        private const int TicksPerPhase = 120;
        private static readonly ItemId ItemA = new(1);
        private static readonly ItemId ItemC = new(3);

        [Test]
        public void ReassemblingFromFullStateOnRebuildKeepsReplayIdentical()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(ItemA);

            // 機械→3マスの直線→チェスト。z=1の左の機械は内部segmentで合流する
            // Machine -> three-cell line -> chest; the machine left of z=1 merges through an internal segment
            Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North)).SetItem(0, ServerContext.ItemStackFactory.Create(ItemA, maxStack));
            for (var z = 0; z <= 2; z++) Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, z), BlockDirection.North);
            Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(-1, 0, 1), BlockDirection.North)).SetItem(0, ServerContext.ItemStackFactory.Create(ItemC, ItemStackLevelDataStore.Instance.GetMaxStack(ItemC)));
            Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 3), BlockDirection.North);
            TickServer();
            var replica = StartReplica();

            var tick = 0;
            var rebuilds = 0;
            var nonEmptyDiffsAfterRebuild = 0;
            RunPhase();

            // 出口のチェストを外してベルトを1マス延ばし、その先にチェストを置く
            // Remove the exit chest, extend the belt by one cell and put the chest beyond it
            ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(0, 0, 3), BlockRemoveReason.ManualRemove);
            Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 3), BlockDirection.North);
            var sink = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 4), BlockDirection.North));
            RunPhase();

            // 合流先のz=1を撤去して直線を2本に割り、内部segmentも消す
            // Remove z=1, splitting the line in two and dropping the internal segment
            ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(0, 0, 1), BlockRemoveReason.ManualRemove);
            RunPhase();

            Assert.AreEqual(2, rebuilds, "one rebuild per topology change");
            Assert.Greater(nonEmptyDiffsAfterRebuild, 0, "diffs were replayed onto re-assembled replicas");
            Assert.Greater(CountOf(sink, ItemA) + CountOf(sink, ItemC), 0, "the extended line delivered");
            BeltFullStateAssert.AreEqual(BeltTransportFullStateCapture.Capture(Datastore().Assembly), replica.CaptureFullState());

            #region Internal

            void RunPhase()
            {
                for (var i = 0; i < TicksPerPhase; i++)
                {
                    tick++;
                    var before = Datastore().Assembly;
                    var diff = TickServer();
                    if (ReferenceEquals(before, Datastore().Assembly))
                    {
                        if (!diff.IsEmpty && rebuilds > 0) nonEmptyDiffsAfterRebuild++;
                        ReplayAndAssert(replica, diff, tick);
                        continue;
                    }
                    replica = Reassemble(replica);
                    rebuilds++;
                }
            }

            BeltTransportReplica Reassemble(BeltTransportReplica old)
            {
                // このtickの差分は新構成の番号で書かれ、tick後の全量に既に含まれる。旧複製へは適用せず全量から組み直す
                // This tick's diff is numbered for the new assembly and already contained in the post-tick full state; never apply it to the old replica, re-assemble from the full state instead
                var full = BeltTransportFullStateCapture.Capture(Datastore().Assembly);
                Assert.AreNotEqual(Signature(old.CaptureFullState()), Signature(full), $"the rebuild at tick {tick} changed the segment shapes");
                var next = BeltTransportReplicaAssembler.Assemble(full);
                Assert.AreEqual(ServerHash(), BeltTransportStateHash.Compute(next.CaptureFullState()), $"hash right after the rebuild at tick {tick}");
                return next;
            }

            #endregion
        }

        // segmentごとのマス数と内部かどうかを並べた、形の比較用の文字列
        // A shape comparison string listing each segment's cell count and whether it is internal
        private static string Signature(BeltTransportFullState full)
        {
            return string.Join(",", full.Segments.Select(s => $"{s.Shape.Kind}:{s.Shape.Cells.Length}:{s.Shape.IsInternal}"));
        }
    }
}
