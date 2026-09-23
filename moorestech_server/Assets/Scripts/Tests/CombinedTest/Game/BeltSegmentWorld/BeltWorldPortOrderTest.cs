using System.Linq;
using Core.Master;
using Game.BeltSegment;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Chest;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.Component;
using Game.Context;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
namespace Tests.CombinedTest.Game.BeltSegmentWorld
{
    public class BeltWorldPortOrderTest
    {
        [TestCase(false)] [TestCase(true)]
        public void DirectionlessOffsetInputPreservesBothBranchReceivers(bool rotated)
        {
            var f = new BeltWorldFixture();
            var branchPos = rotated ? new Vector3Int(0, 0, -2) : new Vector3Int(2, 0, 0);
            var receiverPos = rotated ? new Vector3Int(-2, 0, -3) : new Vector3Int(3, 0, -2);
            var receiverCell = rotated ? new Vector3Int(0, 0, -3) : new Vector3Int(3, 0, 0);
            var secondPos = rotated ? new Vector3Int(-1, 0, -2) : new Vector3Int(2, 0, -1);
            var firstDirection = rotated ? BeltDirection.Back : BeltDirection.Right;
            var secondDirection = rotated ? BeltDirection.Left : BeltDirection.Back;
            var branchBlock = f.Add(ForUnitTestModBlockId.GearBeltConveyorSplitter, branchPos,
                rotated ? BlockDirection.South : BlockDirection.East);
            var offset = f.Add(ForUnitTestModBlockId.OffsetDirectionlessInputChest, receiverPos,
                rotated ? BlockDirection.East : BlockDirection.North).GetComponent<VanillaChestComponent>();
            var second = f.Add(ForUnitTestModBlockId.ChestId, secondPos, BlockDirection.North).GetComponent<VanillaChestComponent>();

            // 接続セルと挿入コンテキストは別々に維持する。
            // Preserve the matched cell independently of the insertion context.
            var connection = branchBlock.GetComponent<IBlockConnectorComponent<IBlockInventory>>().ConnectedTargets[offset];
            Assert.IsNull(connection.TargetConnector);
            Assert.AreEqual(receiverCell, connection.TargetConnectorCell);
            var branch = branchBlock.GetComponent<SegmentBeltComponent>();
            f.Seed(branch, 1); f.Tick(18);
            var snapshot = f.Snapshot();
            Assert.AreEqual(BeltSegmentKind.Branch, snapshot.Simulation.Segments.Single().Kind);
            CollectionAssert.AreEquivalent(new[] { firstDirection, secondDirection },
                snapshot.Simulation.Outputs.Select(o => o.OutputDirection));

            // 次のRRは別inventoryへ。
            // Next RR delivery reaches the other inventory.
            f.Seed(branch, 1); f.Tick(18);
            Assert.AreEqual(1, offset.InventoryItems.Sum(i => i.Count));
            Assert.AreEqual(1, second.InventoryItems.Sum(i => i.Count));
            Assert.AreEqual(0, BeltWorldFixture.Count(f.Snapshot()));
        }
        [TestCase(true)] [TestCase(false)]
        public void MixedMergeKeepsGroupedPriorityAcrossPlacementAndLoad(bool sourceFirst)
        {
            var f = new BeltWorldFixture();
            if (!sourceFirst) f.Belt(Vector3Int.zero, BlockDirection.North);
            var input = f.Belt(Vector3Int.left, BlockDirection.East);
            var chest = f.Add(ForUnitTestModBlockId.ChestId, Vector3Int.back, BlockDirection.North).GetComponent<VanillaChestComponent>();
            if (sourceFirst) f.Belt(Vector3Int.zero, BlockDirection.North);
            f.Seed(input, 1); chest.SetItem(0, new ItemId(2), 3); f.Tick(1);
            var snapshot = f.Snapshot();
            Assert.AreEqual(1, snapshot.Simulation.Links.Length); Assert.AreEqual(1, snapshot.Simulation.Inputs.Length);
            int mergeId = snapshot.Simulation.Inputs[0].TargetSegmentId;
            Assert.AreEqual(BeltDirection.Back, snapshot.Simulation.Inputs[0].InputDirection);
            Assert.AreEqual(BeltDirection.Right, snapshot.Simulation.Links[0].OutputDirection);
            Assert.AreEqual(1, snapshot.Simulation.Segments[mergeId].PriorityIndex);
            string save = f.Save(); var restored = new BeltWorldFixture();
            ((WorldLoaderFromJson)restored.Services.GetRequiredService<IWorldSaveDataLoader>()).Load(save); restored.Belts.Load();
            Assert.AreEqual(new BeltReplaySimulation(snapshot.Simulation).ComputeStateHash(),
                new BeltReplaySimulation(restored.Snapshot().Simulation).ComputeStateHash());
        }
        [TestCase(true)] [TestCase(false)]
        public void MixedBranchKeepsInternalThenExternalOutputOrder(bool reverse)
        {
            var f = new BeltWorldFixture();
            if (!reverse) f.Add(ForUnitTestModBlockId.ChestId, Vector3Int.left, BlockDirection.North);
            f.Belt(Vector3Int.forward, BlockDirection.North);
            var branch = f.Add(ForUnitTestModBlockId.GearBeltConveyorSplitter, Vector3Int.zero, BlockDirection.North).GetComponent<SegmentBeltComponent>();
            if (reverse) f.Add(ForUnitTestModBlockId.ChestId, Vector3Int.left, BlockDirection.North);
            f.Seed(branch, 1); f.Tick(17);
            var before = f.Snapshot(); Assert.AreEqual(1, before.Simulation.Links.Length); Assert.AreEqual(1, before.Simulation.Outputs.Length);
            Assert.AreEqual(BeltDirection.Front, before.Simulation.Links[0].OutputDirection);
            Assert.AreEqual(BeltDirection.Left, before.Simulation.Outputs[0].OutputDirection);
            Assert.AreEqual(1, before.Simulation.Segments.Single(s => s.Kind == BeltSegmentKind.Branch).PriorityIndex);
            string save = f.Save(); var restored = new BeltWorldFixture();
            ((WorldLoaderFromJson)restored.Services.GetRequiredService<IWorldSaveDataLoader>()).Load(save); restored.Belts.Load();
            Assert.AreEqual(new BeltReplaySimulation(before.Simulation).ComputeStateHash(),
                new BeltReplaySimulation(restored.Snapshot().Simulation).ComputeStateHash());
        }
    }
}
