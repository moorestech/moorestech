using Game.Block.Interface.Extension;
using System.Collections.Generic;
using Core.Update;
using Game.BeltSegment;
using Game.Block.Interface;
using NUnit.Framework;
using UniRx;
using UnityEngine;
namespace Tests.CombinedTest.Game.BeltSegmentWorld
{
    public class BeltWorldTickTest
    {
        [Test]
        public void DirtyReadIsCoherentThenReplacementPrecedesFrameAndSequenceResets()
        {
            var f = new BeltWorldFixture(); f.Tick(1); var old = f.Snapshot();
            var events = new List<string>(); BeltWorldSnapshot replacement = null; BeltWorldFrame frame = null;
            f.Belts.OnBeltWorldRebuilt.Subscribe(s => { events.Add("replace"); replacement = s; });
            f.Belts.OnFrame.Subscribe(s => { events.Add("frame"); frame = s; });
            var belt = f.Belt(Vector3Int.zero, BlockDirection.North); f.Seed(belt, 1);
            var first = f.Snapshot(); var second = f.Snapshot();
            Assert.AreEqual(old.Generation, first.Generation); Assert.AreEqual(0, first.Routes.Length);
            Assert.AreEqual(first.Position.Sequence, second.Position.Sequence);
            f.Tick(1); CollectionAssert.AreEqual(new[] { "replace", "frame" }, events);
            Assert.AreEqual(1UL, replacement.Position.Tick); Assert.AreEqual(2U, replacement.Position.Sequence);
            Assert.AreEqual(replacement.Position.Tick, frame.Previous.Tick); Assert.AreEqual(replacement.Position.Sequence, frame.Previous.Sequence);
            Assert.AreEqual(2UL, frame.Position.Tick); Assert.AreEqual(1U, frame.Position.Sequence);
            var replay = new BeltReplaySimulation(replacement.Simulation);
            Assert.AreEqual(replay.ComputeStateHash(), frame.PreviousHash);
            replay.ApplyTick(frame.Replay, false);
            Assert.AreEqual(new BeltReplaySimulation(f.Snapshot().Simulation).ComputeStateHash(), replay.ComputeStateHash());
        }
        [Test]
        public void LoadedTickBeyondUintUsesFullWidthAndDoesNotConsumeReadSequence()
        {
            var f = new BeltWorldFixture(); GameUpdater.RestoreCurrentTick((ulong)uint.MaxValue + 50);
            f.Belts.Load(); var snapshot = f.Snapshot();
            Assert.AreEqual(GameUpdater.CurrentTick, snapshot.Position.Tick); Assert.AreEqual(0, snapshot.Position.Sequence);
            f.Tick(1); Assert.AreEqual((ulong)uint.MaxValue + 51, f.Snapshot().Position.Tick);
            Assert.AreEqual(1, f.Snapshot().Position.Sequence);
        }
        [Test]
        public void HashIncludesAcceptedInputButIgnoresCallerPosition()
        {
            var f = new BeltWorldFixture(); var belt = f.Belt(Vector3Int.zero, BlockDirection.North); f.Seed(belt, 1); f.Tick(1);
            var s = f.Snapshot().Simulation; var before = new BeltReplaySimulation(s).ComputeStateHash();
            var item = s.Segments[0].Items[0].Item;
            item.Position = new ItemPosition(new BeltCell(70, 80, 90), BeltEntryDirection.FromFront, 30);
            s.Segments[0].Items[0] = new BeltItemState(item, s.Segments[0].Items[0].DistanceToExit);
            Assert.AreEqual(before, new BeltReplaySimulation(s).ComputeStateHash());
            item.AcceptedInput = BeltDirection.Left;
            s.Segments[0].Items[0] = new BeltItemState(item, s.Segments[0].Items[0].DistanceToExit);
            Assert.AreNotEqual(before, new BeltReplaySimulation(s).ComputeStateHash());
        }
        [Test]
        public void ActualMachineFramesReplayEveryPhysicalTickAndBoundaryReplacement()
        {
            var f = new BeltWorldFixture();
            var source = f.Add(Tests.Module.TestMod.ForUnitTestModBlockId.ChestId, Vector3Int.back, BlockDirection.North)
                .GetComponent<global::Game.Block.Blocks.Chest.VanillaChestComponent>();
            source.SetItem(0, new global::Core.Master.ItemId(1), 8);
            f.Belt(Vector3Int.zero, BlockDirection.North);
            f.Add(Tests.Module.TestMod.ForUnitTestModBlockId.ChestId, Vector3Int.forward * 2, BlockDirection.North);
            BeltReplaySimulation replay = null; int frames = 0;
            f.Belts.OnBeltWorldRebuilt.Subscribe(s => replay = new BeltReplaySimulation(s.Simulation));
            f.Belts.OnFrame.Subscribe(frame =>
            {
                Assert.AreEqual(replay.ComputeStateHash(), frame.PreviousHash);
                replay.ApplyTick(frame.Replay, false);
                Assert.AreEqual(new BeltReplaySimulation(f.Snapshot().Simulation).ComputeStateHash(), replay.ComputeStateHash());
                frames++;
            });
            f.Tick(20); f.Belt(Vector3Int.forward, BlockDirection.North); f.Tick(120);
            Assert.AreEqual(140, frames);
        }
    }
}
