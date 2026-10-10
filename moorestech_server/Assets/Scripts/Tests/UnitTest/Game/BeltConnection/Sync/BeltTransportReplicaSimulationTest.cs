using System.Linq;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Sync.Replica;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.UnitTest.Game.BeltConnection.Save.BeltSaveTestUtil;
using static Tests.UnitTest.Game.BeltConnection.Sync.BeltSyncTestUtil;
using static Tests.UnitTest.Game.BeltConnection.Topology.BeltTopologyTestUtil;
using static Tests.UnitTest.Game.BeltConnection.Transport.BeltTransportTestUtil;

namespace Tests.UnitTest.Game.BeltConnection.Sync
{
    // 全量から組んだ複製を進めると、機械との搬送が無い限りサーバーの組と毎tick同じ状態になるか
    // Whether a replica assembled from the full state stays identical to the server assembly every tick as long as no machine handoff happens
    public class BeltTransportReplicaSimulationTest
    {
        private const int TickCount = 300;

        [Test]
        public void NetworkWithoutMachineHandoffSimulatesIdentically()
        {
            var world = NewWorld();
            InstallMachineMergePorts();
            PlaceBeltMerge(world, 0);
            PlaceBranch(world, 10);
            PlaceSlope(world, 20);
            PlaceLine(world, 30, 0, 3);
            var machine = Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(29, 0, 1), BlockDirection.North);
            var assembly = Assemble(world);

            // 合流の両入力・分岐の入力・坂・機械合流の内部segmentへ置く。tick中は機械が押し込まないので搬送はベルト間だけ
            // Items on both merge inputs, the branch input, the slope and the machine merge's internal segment; no machine pushes during ticks, so all handoffs are belt-to-belt
            SegmentAt(assembly, new Vector3Int(0, 0, 0)).RestoreItems(new[] { At(ItemA, BeltEntryDirection.FromBack, 0), At(ItemB, BeltEntryDirection.FromBack, 256) });
            SegmentAt(assembly, new Vector3Int(1, 0, 1)).RestoreItems(new[] { At(ItemC, BeltEntryDirection.FromBack, 0) });
            SegmentAt(assembly, new Vector3Int(10, 0, -1)).RestoreItems(new[] { At(ItemA, BeltEntryDirection.FromBack, 0) });
            SegmentAt(assembly, new Vector3Int(20, 0, 0)).RestoreItems(new[] { At(ItemA, BeltEntryDirection.FromBack, 30), At(ItemB, BeltEntryDirection.FromBack, 500) });
            SegmentAt(assembly, new Vector3Int(30, 0, 0)).RestoreItems(new[] { At(ItemB, BeltEntryDirection.FromBack, 0) });
            Assert.IsTrue(Push(assembly, world.GetBlock(new Vector3Int(30, 0, 1)), machine, ItemC));

            var replica = BeltTransportReplicaAssembler.Assemble(BeltTransportFullStateCapture.Capture(assembly));
            var initialHash = HashOf(assembly);
            for (var tick = 1; tick <= TickCount; tick++)
            {
                assembly.Simulation.Tick();
                replica.Simulation.Tick();
                Assert.AreEqual(HashOf(assembly), HashOf(replica), $"hash at tick {tick}");
            }

            // 実際に状態が動いたうえで一致していること、項目ごとにも一致していること
            // The state actually moved, and it also matches field by field
            Assert.AreNotEqual(initialHash, HashOf(assembly), "the server state changed over the run");
            BeltFullStateAssert.AreEqual(BeltTransportFullStateCapture.Capture(assembly), replica.CaptureFullState());
        }

        [Test]
        public void AcceptOnceOnServerHandoffTickKeepsReplicaIdentical()
        {
            var (world, chest) = LineIntoChest();
            var assembly = Assemble(world);
            PutThreeItems(assembly);
            var replica = BeltTransportReplicaAssembler.Assemble(BeltTransportFullStateCapture.Capture(assembly));
            var receiver = MachineReceiverOfOnlyMachineOutput(replica);

            // サーバーを先に進め、アイテムが機械へ出たtickだけ複製の受け手に1回の受け入れを許す
            // Tick the server first and allow the replica receiver exactly one acceptance on the tick an item left into the machine
            var handoffs = 0;
            for (var tick = 1; tick <= TickCount; tick++)
            {
                if (TickServerAndCountHandoff(assembly))
                {
                    receiver.AcceptOnce();
                    handoffs++;
                }
                replica.Simulation.Tick();
                Assert.AreEqual(HashOf(assembly), HashOf(replica), $"hash at tick {tick}");
            }
            Assert.AreEqual(3, handoffs, "three handoffs into the chest");
            Assert.AreEqual(3, TotalCount(Inventory(chest)), "the chest received all three items");
            Assert.AreEqual(0, ItemCount(replica.CaptureFullState()), "the replica is empty as well");
        }

        [Test]
        public void WithoutAcceptOnceReplicaDivergesWhenServerMachineAccepts()
        {
            var (world, _) = LineIntoChest();
            var assembly = Assemble(world);
            PutThreeItems(assembly);
            var replica = BeltTransportReplicaAssembler.Assemble(BeltTransportFullStateCapture.Capture(assembly));

            // 受け入れを許さない複製は出口で待ち続け、サーバーが機械へ渡したtickで食い違う
            // A replica never allowed to accept keeps waiting at the exit and diverges on the tick the server hands off
            var firstHandoffTick = -1;
            var firstDivergenceTick = -1;
            for (var tick = 1; tick <= TickCount && firstDivergenceTick < 0; tick++)
            {
                if (TickServerAndCountHandoff(assembly) && firstHandoffTick < 0) firstHandoffTick = tick;
                replica.Simulation.Tick();
                if (HashOf(assembly) != HashOf(replica)) firstDivergenceTick = tick;
            }
            Assert.Greater(firstHandoffTick, 0, "the server handed an item off");
            Assert.AreEqual(firstHandoffTick, firstDivergenceTick, "divergence starts exactly at the server's handoff");
            Assert.AreEqual(3, ItemCount(replica.CaptureFullState()), "the replica still holds all three items");
        }

        [Test]
        public void RefusingMachineNeedsNoAcceptOnce()
        {
            var (world, chest) = LineIntoChest();
            FillWith(Inventory(chest), ItemC);
            var assembly = Assemble(world);
            PutThreeItems(assembly);
            var replica = BeltTransportReplicaAssembler.Assemble(BeltTransportFullStateCapture.Capture(assembly));

            // 満杯で拒否する機械なら、サーバーも複製も出口で待たせ続けて一致する
            // With a full machine that refuses, both the server and the replica keep the item waiting at the exit and match
            for (var tick = 1; tick <= TickCount; tick++)
            {
                assembly.Simulation.Tick();
                replica.Simulation.Tick();
                Assert.AreEqual(HashOf(assembly), HashOf(replica), $"hash at tick {tick}");
            }
            Assert.AreEqual(3, ItemCount(BeltTransportFullStateCapture.Capture(assembly)), "the server kept all items on the belt");
        }

        // z=0..2 の北向きの直線の先(z=3)に、後ろから受け入れるチェストを置く
        // A chest that accepts from behind placed after a north-facing line z=0..2, at z=3
        private static (IWorldBlockDatastore world, IBlock chest) LineIntoChest()
        {
            var world = NewWorld();
            InstallMachinePorts(new Vector3Int[0], new[] { Vector3Int.back });
            PlaceLine(world, 0, 0, 3);
            var chest = Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 3), BlockDirection.North);
            return (world, chest);
        }

        private static void PutThreeItems(BeltTransportAssembly assembly)
        {
            SegmentAt(assembly, Vector3Int.zero).RestoreItems(new[]
            {
                At(ItemA, BeltEntryDirection.FromBack, 20),
                At(ItemB, BeltEntryDirection.FromBack, 300),
                At(ItemA, BeltEntryDirection.FromBack, 556)
            });
        }

        // サーバーを1tick進め、組からアイテムが減った(=機械へ渡った)かを返す
        // Tick the server once and report whether the assembly lost an item (handed to the machine)
        private static bool TickServerAndCountHandoff(BeltTransportAssembly assembly)
        {
            var before = ItemCount(BeltTransportFullStateCapture.Capture(assembly));
            assembly.Simulation.Tick();
            return ItemCount(BeltTransportFullStateCapture.Capture(assembly)) < before;
        }

        private static BeltReplicaMachineReceiver MachineReceiverOfOnlyMachineOutput(BeltTransportReplica replica)
        {
            var outputs = Enumerable.Range(0, replica.Shapes.Length)
                .SelectMany(i => replica.Shapes[i].Outputs.Where(o => o.IsMachine).Select(o => (index: i, direction: o.Direction)))
                .ToList();
            Assert.AreEqual(1, outputs.Count, "machine outputs in the replica");
            return replica.MachineReceiverOf(outputs[0].index, outputs[0].direction);
        }
    }
}
