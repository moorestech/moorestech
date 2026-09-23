using System.Linq;
using Core.Master;
using Game.BeltSegment;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Chest;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
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
