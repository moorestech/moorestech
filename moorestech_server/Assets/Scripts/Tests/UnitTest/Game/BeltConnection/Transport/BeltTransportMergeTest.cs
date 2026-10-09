using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.UnitTest.Game.BeltConnection.Topology.BeltTopologyTestUtil;
using static Tests.UnitTest.Game.BeltConnection.Transport.BeltTransportTestUtil;

namespace Tests.UnitTest.Game.BeltConnection.Transport
{
    public class BeltTransportMergeTest
    {
        private static readonly ItemId ItemA = new(1);
        private static readonly ItemId ItemB = new(2);

        [Test]
        public void TwoMachinesIntoMergeDeliverLeftFirst()
        {
            var world = NewWorld();
            InstallMachinePorts(new[] { Vector3Int.left, Vector3Int.right }, new[] { Vector3Int.back });
            var leftMachine = Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(-1, 0, 1), BlockDirection.North);
            var rightMachine = Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(1, 0, 1), BlockDirection.North);
            var mergeBelt = Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 2), BlockDirection.North);
            var chest = Inventory(Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 3), BlockDirection.North));
            var assembly = Assemble(world);

            Assert.IsTrue(Push(assembly, mergeBelt, leftMachine, ItemA));
            Assert.IsTrue(Push(assembly, mergeBelt, rightMachine, ItemB));

            // 合流の搬入順は直進→左→右なので、左の機械のアイテムが先に届く
            // Merge input order starts straight, then left, then right, so the left machine's item arrives first
            var arrivalA = -1;
            var arrivalB = -1;
            for (var tick = 1; tick <= 400 && (arrivalA < 0 || arrivalB < 0); tick++)
            {
                assembly.Simulation.Tick();
                if (arrivalA < 0 && CountOf(chest, ItemA) == 1) arrivalA = tick;
                if (arrivalB < 0 && CountOf(chest, ItemB) == 1) arrivalB = tick;
            }
            Assert.Greater(arrivalA, 0, "left machine item arrived");
            Assert.Greater(arrivalB, 0, "right machine item arrived");
            Assert.Less(arrivalA, arrivalB, "left machine item arrives before the right one");
            Assert.AreEqual(2, TotalCount(chest));
        }

        [Test]
        public void BranchNextToMergeIsNotStarved()
        {
            var world = NewWorld();
            InstallMachinePorts(new[] { Vector3Int.forward, Vector3Int.left }, new[] { Vector3Int.back, Vector3Int.left, Vector3Int.right });

            // 分配器の左右はそれぞれベルト1マスでチェストへ、正面は横から入るベルトと合流する
            // The splitter's sides each run one belt cell into a chest; its front meets a belt entering from the side
            var splitterMachine = Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North);
            var splitter = Place(world, ForUnitTestModBlockId.GearBeltConveyorSplitter, new Vector3Int(0, 0, 0), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(-1, 0, 0), BlockDirection.West);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(1, 0, 0), BlockDirection.East);
            var westChest = Inventory(Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(-2, 0, 0), BlockDirection.North));
            var eastChest = Inventory(Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(2, 0, 0), BlockDirection.North));
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North);
            var sideBelt = Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(1, 0, 1), BlockDirection.West);
            var sideMachine = Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(2, 0, 1), BlockDirection.North);
            Place(world, ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 2), BlockDirection.North);
            var mergeChest = Inventory(Place(world, ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 3), BlockDirection.North));
            var assembly = Assemble(world);

            // 毎tick両方の機械から押し込みを試みる。満杯で拒否されても構わない
            // Every tick both machines try to push; rejections while full are fine
            for (var tick = 0; tick < 1500; tick++)
            {
                Push(assembly, splitter, splitterMachine, ItemA);
                Push(assembly, sideBelt, sideMachine, ItemB);
                assembly.Simulation.Tick();
            }

            // 合流先には分岐側(A)と横ベルト側(B)の両方が届き、分岐の左右にもAが届く
            // The merge's chest gets both the branch side (A) and the side belt (B); the splitter's sides get A too
            Assert.Greater(CountOf(mergeChest, ItemA), 0, "branch-side item reaches the merge chest");
            Assert.Greater(CountOf(mergeChest, ItemB), 0, "side-belt item reaches the merge chest");
            Assert.Greater(CountOf(westChest, ItemA), 0, "west branch delivers");
            Assert.Greater(CountOf(eastChest, ItemA), 0, "east branch delivers");
            Assert.AreEqual(0, CountOf(westChest, ItemB) + CountOf(eastChest, ItemB), "side-belt item never reaches the branch chests");
        }
    }
}
