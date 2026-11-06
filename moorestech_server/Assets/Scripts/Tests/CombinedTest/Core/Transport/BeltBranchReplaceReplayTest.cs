using System;
using System.IO;
using System.Linq;
using Core.Item;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Sync.Replica;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Gear.Common;
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
    // 2クリック設置の分岐と同じ手順(直線の1マスを撤去→分岐器と横の経路を同じtickで設置)の後、複製が横の経路へ流れる荷物をサーバーと同じに見せるか
    // After the same steps as the two-click branch (remove one cell of a line, then place the splitter and the side route in one tick), does the replica show items flowing down the side route like the server
    public class BeltBranchReplaceReplayTest
    {
        private const int FlowTicks = 600;
        private const int SettleTicks = 5;
        private const int BranchTicks = 400;
        private static readonly ItemId ItemA = new(1);
        private static readonly Vector3Int SplitterCell = new(0, 0, 2);

        [TestCase(false)]
        [TestCase(true)]
        public void SideRouteCarriesItemsRightAfterTheBranchIsPlaced(bool gearFamily)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var straight = gearFamily ? ForUnitTestModBlockId.WoodGearBelt : ForUnitTestModBlockId.BeltConveyorId;
            var splitter = gearFamily ? ForUnitTestModBlockId.WoodGearBeltSplitter : ForUnitTestModBlockId.BeltConveyorSplitter;
            var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(ItemA);

            // チェスト→北向き5マス→チェスト。歯車ベルト(本番と同じく分岐器の速度がベルトと同じ木のファミリー)は先頭マスの横に発電機
            // Chest -> five north-facing cells -> chest; gear belts (the wood family, whose splitter shares the belt speed like production) get a generator beside the first cell
            Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North)).SetItem(0, ServerContext.ItemStackFactory.Create(ItemA, maxStack));
            for (var z = 0; z <= 4; z++) Assert.IsNotNull(Place(straight, new Vector3Int(0, 0, z), BlockDirection.North), $"belt at z={z}");
            var mainSink = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 5), BlockDirection.North));
            if (gearFamily) Assert.IsNotNull(Place(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, new Vector3Int(-1, 0, 0), BlockDirection.North), "generator");
            TickServer();
            var replica = StartReplica();
            var tick = 0;
            var routeItemTicks = 0;
            while (CountOf(mainSink, ItemA) == 0 && tick < FlowTicks) RunPhase(1);
            Assert.Greater(CountOf(mainSink, ItemA), 0, "the line delivers before the branch");

            // クライアントの分岐確定と同じ順: 既存ベルトの撤去が受理されてから、分岐器→経路の順で設置
            // Same order as the client's branch commit: the existing belt's removal first, then the splitter followed by the route cells
            Assert.IsTrue(ServerContext.WorldBlockDatastore.RemoveBlock(SplitterCell, BlockRemoveReason.ManualRemove), "remove the belt under the splitter");
            RunPhase(SettleTicks);
            Assert.IsNotNull(Place(splitter, SplitterCell, BlockDirection.North), "splitter");
            Assert.IsNotNull(Place(straight, new Vector3Int(1, 0, 2), BlockDirection.East), "route cell 1");
            Assert.IsNotNull(Place(straight, new Vector3Int(2, 0, 2), BlockDirection.East), "route cell 2");
            var branchSink = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(3, 0, 2), BlockDirection.North));

            var mainBefore = CountOf(mainSink, ItemA);
            RunPhase(BranchTicks);

            // 分岐器は前と横の2出力を持ち、横の経路に荷物が見え、両方のチェストへ届く
            // The splitter has two outputs (front and side), items are visible on the side route, and both chests receive
            var branchShape = replica.Shapes[ReplicaSegmentIndexAt(replica, SplitterCell)];
            var routeIndex = ReplicaSegmentIndexAt(replica, new Vector3Int(1, 0, 2));
            Debug.Log($"[BeltBranchReplaceReplay] gear={gearFamily} splitterOutputs={string.Join(",", branchShape.Outputs.Select(o => o.Direction + ">" + o.PartnerSegmentIndex))} routeSegment={routeIndex} routeSpeed={replica.Segments[routeIndex].Speed} routeInputs={string.Join(",", replica.Shapes[routeIndex].Inputs.Select(i => i.Direction + "<" + i.PartnerSegmentIndex))} branchSpeed={replica.Segments[ReplicaSegmentIndexAt(replica, SplitterCell)].Speed} priority={replica.Segments[ReplicaSegmentIndexAt(replica, SplitterCell)].PriorityOrder}");
            Assert.AreEqual(2, branchShape.Outputs.Length, "splitter outputs in the replica: " + string.Join(",", branchShape.Outputs.Select(o => o.Direction)));
            Assert.Greater(routeItemTicks, 0, "the replica showed items on the side route");
            Assert.Greater(CountOf(branchSink, ItemA), 0, "the side route delivered");
            Assert.Greater(CountOf(mainSink, ItemA), mainBefore, "the front still delivers");
            Debug.Log($"[BeltBranchReplaceReplay] gear={gearFamily} main={CountOf(mainSink, ItemA)} branch={CountOf(branchSink, ItemA)} routeItemTicks={routeItemTicks}");
            BeltFullStateAssert.AreEqual(Datastore().CaptureFullState(), BeltTransportFullStateCapture.Capture(replica.Shapes, replica.Segments));

            #region Internal

            // 作り直したtickは複製を全量から組み直し、それ以外は差分を再生してハッシュを照合する(BeltTransportDiffRebuildTestと同じ)
            // On a rebuilt tick re-assemble the replica from the full state, otherwise replay the diff and check the hash (as BeltTransportDiffRebuildTest does)
            // 横の経路のマスが複製にある間は、そのsegmentに荷物が見えたtickを数える(クライアントの表示はこの複製を読む)
            // While the side route's cell exists in the replica, count the ticks on which its segment showed items (the client view reads this replica)
            void RunPhase(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    tick++;
                    var before = Datastore().Assembly;
                    var diff = TickServer();
                    if (ReferenceEquals(before, Datastore().Assembly)) ReplayAndAssert(replica, diff, tick);
                    else
                    {
                        replica = BeltTransportReplicaAssembler.Assemble(Datastore().CaptureFullState());
                        Assert.AreEqual(ServerHash(), BeltTransportStateHash.Compute(replica.Shapes, replica.Segments), $"hash right after the rebuild at tick {tick}");
                    }
                    if (TryReplicaSegmentIndexAt(replica, new Vector3Int(1, 0, 2), out var routeSegment) && 0 < replica.Segments[routeSegment].CaptureItems().Length) routeItemTicks++;
                }
            }

            #endregion
        }

        // 本番マスタ(歯車コネクタに形状がある)で、分岐の最初のセルが横へ真っ直ぐ出るか、すぐ曲がって本線と平行になるかで、先頭セルが分岐器の歯車網に繋がるかを確かめる
        // With the production master (gear connectors carry shapes), checks whether the route's first cell joins the splitter's gear network when it leaves straight sideways versus turning at once to run parallel to the line
        // 経路segmentの速度は先頭セルの回転だけで決まる(BeltGearDrive)ので、先頭が網から切れた経路は速度0のまま荷物を受け入れて止める
        // A route segment's speed comes from its head cell's rotation alone (BeltGearDrive), so a route whose head is cut from the network accepts items at speed 0 and stops them
        [Test]
        public void ProductionGearSplitterLinksStraightSideRouteButNotTurnedOne()
        {
            // テストはクライアントプロジェクトを作業ディレクトリに走り、マスタのリポジトリは本リポジトリの隣(.moorestech-external-revisions.json の ../moorestech_master、CIも同じ配置)にある
            // Tests run with the client project as the working directory; the master repository sits beside this repository (../moorestech_master per .moorestech-external-revisions.json, same layout in CI)
            var alphaMod = Path.Combine(Environment.CurrentDirectory, "../", "../", "moorestech_master", "server_v8");
            Assert.IsTrue(Directory.Exists(Path.Combine(alphaMod, "mods")), $"alpha mod not found at {alphaMod}");
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(alphaMod));
            var straight = MasterHolder.BlockMaster.GetBlockId(Guid.Parse("7743a779-1d62-4b94-b306-4a0670bd8b48"));
            var splitter = MasterHolder.BlockMaster.GetBlockId(Guid.Parse("3d18ea68-93e7-4dc4-b5eb-fa3e0e5e45d9"));
            Assert.IsTrue(BeltSplitterCounterpart.TryGetSplitterBlockId(straight, out var counterpart) && counterpart == splitter, "the splitter is the straight belt's counterpart");

            for (var z = 0; z <= 4; z++) Assert.IsNotNull(Place(straight, new Vector3Int(0, 0, z), BlockDirection.North), $"belt at z={z}");
            Assert.IsTrue(ServerContext.WorldBlockDatastore.RemoveBlock(SplitterCell, BlockRemoveReason.ManualRemove));
            var splitterBlock = Place(splitter, SplitterCell, BlockDirection.North);
            // 右(東)へ真っ直ぐ出る経路と、左(西)へ出てすぐ北へ曲がる経路
            // A route leaving straight to the right (east) and one leaving left (west) that turns north at once
            var straightHead = Place(straight, new Vector3Int(1, 0, 2), BlockDirection.East);
            Place(straight, new Vector3Int(2, 0, 2), BlockDirection.East);
            var turnedHead = Place(straight, new Vector3Int(-1, 0, 2), BlockDirection.North);
            var turnedSecond = Place(straight, new Vector3Int(-1, 0, 3), BlockDirection.North);
            TickServer();

            Assert.AreEqual(NetworkOf(splitterBlock), NetworkOf(straightHead), "a side route leaving straight joins the splitter's gear network (chain to side)");
            Assert.AreEqual(NetworkOf(turnedHead), NetworkOf(turnedSecond), "the turned route is linked within itself");
            Assert.AreNotEqual(NetworkOf(splitterBlock), NetworkOf(turnedHead), "a side route that turns at once is cut from the splitter's gear network (side to side is not connectable)");

            // 本線の横に東向きのセルを足すと、その鎖が本線の横と、その横が曲がった経路の鎖と繋がり、曲がった経路も網に入る
            // Adding an east-facing cell beside the line links its chain to the line's side and its side to the turned route's chain, so the turned route joins the network
            Place(straight, new Vector3Int(-1, 0, 4), BlockDirection.East);
            TickServer();
            Assert.AreEqual(NetworkOf(splitterBlock), NetworkOf(turnedHead), "a later cell touching the turned route with a chain-to-side link powers it");
        }

        private static GearNetworkId NetworkOf(IBlock block)
        {
            Assert.IsTrue(ServerContext.GetService<IGearNetworkDatastore>().TryGetGearNetwork(block.BlockInstanceId, out var network), $"gear network of {block.BlockPositionInfo.OriginalPos}");
            return network.NetworkId;
        }

        private static int ReplicaSegmentIndexAt(BeltTransportReplica replica, Vector3Int cell)
        {
            Assert.IsTrue(TryReplicaSegmentIndexAt(replica, cell, out var index), $"no replica segment holds {cell}");
            return index;
        }

        private static bool TryReplicaSegmentIndexAt(BeltTransportReplica replica, Vector3Int cell, out int index)
        {
            for (index = 0; index < replica.Shapes.Length; index++)
                if (replica.Shapes[index].Cells.Any(c => c.Position == cell)) return true;
            return false;
        }
    }
}
