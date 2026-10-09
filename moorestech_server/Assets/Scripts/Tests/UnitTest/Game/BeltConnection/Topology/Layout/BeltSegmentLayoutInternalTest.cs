using System.Collections.Generic;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Topology.Layout;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.InventoryConnectsModule;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.UnitTest.Game.BeltConnection.Machine;
using UnityEngine;
using static Tests.UnitTest.Game.BeltConnection.Topology.BeltTopologyTestUtil;
using static Tests.UnitTest.Game.BeltConnection.Topology.Layout.BeltSegmentLayoutTestUtil;

namespace Tests.UnitTest.Game.BeltConnection.Topology.Layout
{
    public class BeltSegmentLayoutInternalTest
    {
        private const int Machine = BeltSegmentLayoutLink.Machine;

        [Test]
        public void MachineIntoMergeGetsInternalSegmentAfterMerge()
        {
            var world = NewWorld();
            var rightPort = InstallMachinePorts(Vector3Int.right);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 0), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North);
            var machine = Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(-1, 0, 1), BlockDirection.North);
            var layouts = BuildLayouts(world);

            // 後ろの通常segmentは合流へ直結し、機械入力だけが合流直後の内部segmentを経由する
            // The rear normal segment feeds the merge directly; only the machine input goes through the internal segment right after the merge
            Assert.AreEqual(3, layouts.Count);
            AssertSegment(layouts[0], BeltSegmentKind.Normal, 6, BeltDirection.Front, new Vector3Int(0, 0, 0));
            AssertSegment(layouts[1], BeltSegmentKind.Merge, 6, BeltDirection.Front, new Vector3Int(0, 0, 1));
            AssertLink(layouts[0].Outputs[0], BeltDirection.Front, BeltEntryDirection.FromBack, 1);
            Assert.AreEqual(2, layouts[1].Inputs.Length);
            AssertLink(layouts[1].Inputs[0], BeltDirection.Back, BeltEntryDirection.FromBack, 0);
            AssertLink(layouts[1].Inputs[1], BeltDirection.Left, BeltEntryDirection.FromLeft, 2);
            Assert.IsEmpty(layouts[1].Outputs);

            // 内部segmentは機械側から合流側へ右向きに進む
            // The internal segment travels right, from the machine side into the merge
            var inner = layouts[2];
            AssertInternal(inner, BeltDirection.Right);
            AssertLink(inner.Inputs[0], BeltDirection.Left, BeltEntryDirection.FromLeft, Machine);
            Assert.IsTrue(inner.Inputs[0].IsMachine);
            Assert.AreSame(machine, inner.Inputs[0].Connection.PartnerBlock);
            Assert.AreSame(rightPort, inner.Inputs[0].Connection.SourceConnector);
            AssertLink(inner.Outputs[0], BeltDirection.Right, BeltEntryDirection.FromLeft, 1);
            Assert.IsFalse(inner.Outputs[0].IsMachine);
        }

        [Test]
        public void TwoMachinesIntoMergeGetInternalsInDirectionOrder()
        {
            var world = NewWorld();
            InstallMachinePorts(Vector3Int.left, Vector3Int.right);
            Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(1, 0, 1), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(-1, 0, 1), BlockDirection.North);
            var layouts = BuildLayouts(world);

            // 合流の直後に Left → Right の順で内部segmentが並ぶ
            // Internal segments follow the merge in Left, then Right order
            Assert.AreEqual(3, layouts.Count);
            AssertSegment(layouts[0], BeltSegmentKind.Merge, 6, BeltDirection.Front, new Vector3Int(0, 0, 1));
            AssertLink(layouts[0].Inputs[0], BeltDirection.Left, BeltEntryDirection.FromLeft, 1);
            AssertLink(layouts[0].Inputs[1], BeltDirection.Right, BeltEntryDirection.FromRight, 2);
            AssertInternal(layouts[1], BeltDirection.Right);
            AssertInternal(layouts[2], BeltDirection.Left);
            Assert.IsTrue(layouts[1].Inputs[0].IsMachine);
            Assert.IsTrue(layouts[2].Inputs[0].IsMachine);
            AssertLink(layouts[1].Outputs[0], BeltDirection.Right, BeltEntryDirection.FromLeft, 0);
            AssertLink(layouts[2].Outputs[0], BeltDirection.Left, BeltEntryDirection.FromRight, 0);
        }

        [Test]
        public void MachineIntoSingleInputCellLinksDirectly()
        {
            var world = NewWorld();
            var rightPort = InstallMachinePorts(Vector3Int.right);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(-1, 0, 1), BlockDirection.North);
            var layouts = BuildLayouts(world);

            Assert.AreEqual(1, layouts.Count);
            AssertSegment(layouts[0], BeltSegmentKind.Normal, 6, BeltDirection.Front, new Vector3Int(0, 0, 1));
            Assert.AreEqual(1, layouts[0].Inputs.Length);
            AssertLink(layouts[0].Inputs[0], BeltDirection.Left, BeltEntryDirection.FromLeft, Machine);
            Assert.AreSame(rightPort, layouts[0].Inputs[0].Connection.SourceConnector);
        }

        [Test]
        public void BranchIntoMergeGoesThroughInternalSegment()
        {
            var world = NewWorld();
            Place(world, ForUnitTestModBlockId.GearBeltConveyorSplitter, Vector3Int.zero, BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(1, 0, 1), BlockDirection.West);
            var layouts = BuildLayouts(world);

            Assert.AreEqual(4, layouts.Count);
            AssertSegment(layouts[0], BeltSegmentKind.Branch, 128, BeltDirection.Front, Vector3Int.zero);
            AssertSegment(layouts[1], BeltSegmentKind.Merge, 6, BeltDirection.Front, new Vector3Int(0, 0, 1));
            AssertSegment(layouts[3], BeltSegmentKind.Normal, 6, BeltDirection.Left, new Vector3Int(1, 0, 1));

            // 分岐の出力と合流の入力はどちらも合流本体ではなく内部segmentを指す
            // Both the branch output and the merge input point at the internal segment, not at each other
            Assert.AreEqual(1, layouts[0].Outputs.Length);
            AssertLink(layouts[0].Outputs[0], BeltDirection.Front, BeltEntryDirection.FromBack, 2);
            Assert.AreEqual(2, layouts[1].Inputs.Length);
            AssertLink(layouts[1].Inputs[0], BeltDirection.Back, BeltEntryDirection.FromBack, 2);
            AssertLink(layouts[1].Inputs[1], BeltDirection.Right, BeltEntryDirection.FromRight, 3);
            AssertInternal(layouts[2], BeltDirection.Front);
            AssertLink(layouts[2].Inputs[0], BeltDirection.Back, BeltEntryDirection.FromBack, 0);
            AssertLink(layouts[2].Outputs[0], BeltDirection.Front, BeltEntryDirection.FromBack, 1);
            AssertLink(layouts[3].Outputs[0], BeltDirection.Left, BeltEntryDirection.FromRight, 1);
        }

        [Test]
        public void MergeChainWithMachineIsPlacementOrderIndependent()
        {
            var forward = BuildInOrder(new[] { 0, 1, 2, 3, 4, 5 }, out var layouts);
            var reverse = BuildInOrder(new[] { 5, 4, 3, 2, 1, 0 }, out _);
            CollectionAssert.AreEqual(forward, reverse);

            // 合流(0,0,1)から合流(0,0,2)へは合流bufferなので内部segmentを挟まず直結する
            // Merge (0,0,1) feeds merge (0,0,2) directly because a merge buffer needs no internal segment
            Assert.AreEqual(6, layouts.Count);
            AssertSegment(layouts[2], BeltSegmentKind.Merge, 6, BeltDirection.Front, new Vector3Int(0, 0, 1));
            AssertSegment(layouts[3], BeltSegmentKind.Merge, 6, BeltDirection.Front, new Vector3Int(0, 0, 2));
            Assert.AreEqual(3, layouts[2].Inputs.Length);
            AssertLink(layouts[2].Inputs[0], BeltDirection.Back, BeltEntryDirection.FromBack, 1);
            AssertLink(layouts[2].Inputs[1], BeltDirection.Left, BeltEntryDirection.FromLeft, 0);
            AssertLink(layouts[2].Inputs[2], BeltDirection.Right, BeltEntryDirection.FromRight, 5);
            AssertLink(layouts[2].Outputs[0], BeltDirection.Front, BeltEntryDirection.FromBack, 3);
            AssertLink(layouts[3].Inputs[0], BeltDirection.Back, BeltEntryDirection.FromBack, 2);
            AssertLink(layouts[3].Inputs[1], BeltDirection.Left, BeltEntryDirection.FromLeft, 4);
            AssertInternal(layouts[4], BeltDirection.Right);
            Assert.IsTrue(layouts[4].Inputs[0].IsMachine);

            List<string> BuildInOrder(int[] order, out List<BeltSegmentLayout> built)
            {
                var world = NewWorld();
                InstallMachinePorts(Vector3Int.right);
                var positions = new[] { new Vector3Int(0, 0, 0), new Vector3Int(-1, 0, 1), new Vector3Int(1, 0, 1), new Vector3Int(0, 0, 1), new Vector3Int(0, 0, 2) };
                var directions = new[] { BlockDirection.North, BlockDirection.East, BlockDirection.West, BlockDirection.North, BlockDirection.North };
                // 0〜4: ベルト 5: (0,0,2)の左へ出す機械
                // 0-4: belts, 5: machine emitting into the left of (0,0,2)
                foreach (var index in order)
                {
                    if (index == 5) Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(-1, 0, 2), BlockDirection.North);
                    else Place(world, ForUnitTestModBlockId.BeltConveyorId, positions[index], directions[index]);
                }
                built = BuildLayouts(world);
                return Signature(built);
            }
        }

        private static IWorldBlockDatastore NewWorld()
        {
            return new BeltEdgeTestWorld(false, BlockDirection.North).World;
        }

        private static OutputConnectsElement InstallMachinePorts(params Vector3Int[] directions)
        {
            // 1マス機械に、指定方向ごとの出力ポートを付ける。戻り値は最後のポート
            // Give the one-cell machine one output port per direction; returns the last port
            var ports = new OutputConnectsElement[directions.Length];
            for (var i = 0; i < directions.Length; i++) ports[i] = MachinePortTestTemplate.Output(Vector3Int.zero, new[] { directions[i] }, null);
            MachinePortTestTemplate.Install(new InventoryConnects(null, ports), Vector3Int.one);
            return ports[ports.Length - 1];
        }
    }
}
