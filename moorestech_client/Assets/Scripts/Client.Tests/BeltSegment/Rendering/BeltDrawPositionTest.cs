using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using System;
using System.Linq;
using Client.Game.InGame.BeltSegment.Gpu;
using Client.Game.InGame.BeltSegment.Rendering;
using Game.BeltSegment;
using NUnit.Framework;
using UnityEngine;
namespace Client.Tests.BeltSegment.Rendering
{
    public sealed class BeltDrawPositionTest
    {
        [TestCase(192, 0.5f, -0.25f)][TestCase(64, 0.5f, 0.25f)]
        public void StraightInterpolatesAcrossBoundary(int distance, float x, float z)
            => AssertPoint(new BeltRoute(new[] { Cell(0,0,0,0) }, Entries(Cell(0,-1,0,0))), distance, new Vector3(x,0.48f,z));
        [TestCase(192, 0.75f, 0.5f)][TestCase(64, 1.25f, 0.5f)]
        public void CornerFollowsPreviousCellIntoCurrent(int distance, float x, float z)
            => AssertPoint(new BeltRoute(new[] { Cell(0,0,0,0), Cell(1,0,0,0) }, Entries(Cell(0,-1,0,0))), distance, new Vector3(x,0.48f,z));
        [TestCase(256, 0f, 0f)][TestCase(128, 0f, 0.5f)][TestCase(0, 0.5f, 1f)]
        public void UpSlopeUsesSharedPortHeight(int distance, float height, float forward)
        {
            var route = new BeltRoute(new[] { Cell(0,0,0,0), Cell(0,1,1,0) }, Entries(Cell(0,-1,0,0)));
            AssertPoint(route, distance, new Vector3(0.5f, 0.48f + height, 0.5f + forward));
        }
        [TestCase(256, 1f, 0f)][TestCase(128, 1f, 0.5f)][TestCase(0, 0.5f, 1f)]
        public void DownSlopeUsesSharedPortHeight(int distance, float height, float forward)
        {
            var route = new BeltRoute(new[] { Cell(0,0,2,2), Cell(0,1,1,2) }, Entries(Cell(0,-1,2,2)));
            AssertPoint(route, distance, new Vector3(0.5f, 0.48f + height, 0.5f + forward));
        }
        [TestCase(BeltDirection.Back, 0.5f, 0f)][TestCase(BeltDirection.Left, 0f, 0.5f)]
        public void RestoredMergeUsesAcceptedInput(BeltDirection direction, float x, float z)
        {
            var entries = Entries(Cell(0,-1,0,0)); entries[(int)BeltDirection.Left] = Cell(-1,0,0,0);
            var route = new BeltRoute(new[] { Cell(0,0,0,0) }, entries);
            var item = Item(7, direction, 128);
            using var fixture = new DrawFixture(new[] { route }, new[] { BeltReplaySegmentState.Merge(16, 0, new[] { item }, null) });
            Assert.That(Vector3.Distance(fixture.Positions()[0], new Vector3(x, 0.48f, z)), Is.LessThan(0.0001f));
        }
        [Test]
        public void LinearScanCrosses128AndGroupsSparseKindsWithRingWrap()
        {
            const int capacity = 320, count = 259, head = 310;
            var route = new BeltRoute(Enumerable.Range(0,capacity).Select(i => Cell(0,i,0,0)).ToArray(), Entries(Cell(0,-1,0,0)));
            var distances = new int[count];
            int accumulatedGap = 0;
            for (int i = 0; i < count; i++)
            {
                accumulatedGap += 3 + i % 23;
                distances[i] = i * 256 + accumulatedGap;
            }
            var items = Enumerable.Range(0,count).Select(i => Item(i % 2 == 0 ? 7 : 900000, BeltDirection.Back, distances[i])).ToArray();
            using var f = new DrawFixture(new[] { route }, new[] { BeltReplaySegmentState.Normal(capacity,16,items) });
            var state = new GpuBeltState[1]; f.Simulation.Buffers.States.GetData(state);
            var gaps = new int[capacity]; var values = new GpuBeltItem[capacity];
            for (int i = 0; i < count; i++)
            {
                int physical = (i + head) % capacity;
                gaps[physical] = 3 + i % 23;
                values[physical] = new GpuBeltItem { Kind = items[i].Item.ItemId, AcceptedInput = (int)BeltDirection.Back };
            }
            state[0].Head = head; f.Simulation.Buffers.States.SetData(state);
            f.Simulation.Buffers.Gaps.SetData(gaps); f.Simulation.Buffers.Items.SetData(values); f.Dispatch.Recompute();
            CollectionAssert.AreEqual(new uint[] {130,129}, f.Counts());
            var positions = f.Positions().OrderByDescending(v => v.z).ToArray();
            // 127/128/255隙間累積比較。
            // Compare cumulative gaps at 127/128/255.
            for(int i=0;i<count;i++)
                Assert.That(positions[i].z, Is.EqualTo(capacity-0.5f-distances[i]/256f).Within(0.0001f), $"logical item {i}");
        }
        [Test]
        public void Segment129IsDrawnAndJunctionBufferIsHidden()
        {
            var routes = Enumerable.Range(0,129).Select(i => new BeltRoute(new[] {Cell(i,0,0,0)},Entries(Cell(i,-1,0,0)))).ToArray();
            var states = Enumerable.Range(0,129).Select(i => BeltReplaySegmentState.Branch(1,16,0,new[] {Item(7,BeltDirection.Back,0)},Item(900000,BeltDirection.Back,0).Item)).ToArray();
            using var f = new DrawFixture(routes,states);
            CollectionAssert.AreEqual(new uint[] {129,0}, f.Counts());
            Assert.That(f.Positions().Max(v=>v.x),Is.EqualTo(128.5f));
        }
        [TestCase("upstream")][TestCase("merge")][TestCase("corner")][TestCase("unconnected")]
        public void RealTopologyRemovalAndReloadKeepLocalCpuAndGpuEntry(string shape)
        {
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            GameUpdater.RestoreCurrentTick(0);
            var world = services.GetRequiredService<BeltWorldDatastore>();
            var origin = new Vector3Int(20, 0, 30);
            var targetPosition = origin + Vector3Int.forward;
            var removedPosition = origin;
            var expectedEntry = BeltDirection.Back;
            if (shape == "corner")
            {
                Add(origin, BlockDirection.North);
                removedPosition = origin + Vector3Int.forward;
                Add(removedPosition, BlockDirection.East);
                targetPosition = removedPosition + Vector3Int.right;
                expectedEntry = BeltDirection.Left;
            }
            else if (shape != "unconnected") Add(origin, BlockDirection.North);
            var target = Add(targetPosition, BlockDirection.North);
            if (shape == "merge") Add(targetPosition + Vector3Int.left, BlockDirection.East);
            target.SetItem(0, ServerContext.ItemStackFactory.Create(new ItemId(7), 1));
            for (int tick = 0; tick < 3; tick++) GameUpdater.Update();
            var before = world.CaptureCell(target).RunningItem;
            Assert.Less(before.Progress, 128);
            if (shape != "unconnected") ServerContext.WorldBlockDatastore.RemoveBlock(removedPosition, BlockRemoveReason.ManualRemove);
            BeltWorldSnapshot boundary = null;
            using var subscription = world.OnBeltWorldRebuilt.Subscribe(value => boundary = value);
            if (shape == "unconnected") boundary = world.CaptureSnapshot();
            else GameUpdater.Update();
            Assert.NotNull(boundary);
            var survivor = boundary.Simulation.Segments.SelectMany(segment => segment.Items).Single();
            Assert.AreEqual(before.TransportGuid, survivor.Item.Guid);
            Assert.AreEqual(expectedEntry, survivor.Item.AcceptedInput);
            Assert.AreEqual(BeltConstants.ItemWidth - before.Progress, survivor.DistanceToExit);
            AssertPosition(boundary, before.Progress);
            // 保存と実loadも同じ入口を保持し、古いsegmentの向きへ戻らない。
            // Production save/load must retain the local entry instead of reverting to the old segment head.
            var saved = world.CaptureCell(target).RunningItem;
            string text = services.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson();
            var (_, loaded) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ((WorldLoaderFromJson)loaded.GetRequiredService<IWorldSaveDataLoader>()).Load(text);
            var restored = loaded.GetRequiredService<BeltWorldDatastore>(); restored.Load();
            Assert.AreEqual(saved.TransportGuid, restored.CaptureSnapshot().Simulation.Segments.SelectMany(segment => segment.Items).Single().Item.Guid);
            AssertPosition(restored.CaptureSnapshot(), saved.Progress);
            #region Internal
            SegmentBeltComponent Add(Vector3Int position, BlockDirection direction)
            {
                Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, position, direction, Array.Empty<BlockCreateParam>(), out var block));
                return block.GetComponent<SegmentBeltComponent>();
            }
            void AssertPosition(BeltWorldSnapshot snapshot, int progress)
            {
                using var draw = new DrawFixture(snapshot.Routes, snapshot.Simulation.Segments);
                var offset = shape == "merge" || expectedEntry == BeltDirection.Left ? Vector3.left : Vector3.back;
                var expected = (Vector3)targetPosition + offset * (1 - progress / (float)BeltConstants.ItemWidth) + new Vector3(0.5f, 0.48f, 0.5f);
                Assert.That(Vector3.Distance(draw.Positions().Single(), expected), Is.LessThan(0.0001f));
                Assert.AreEqual(1, snapshot.Simulation.Segments.Sum(segment => segment.Items.Length));
            }
            #endregion
        }
        [TestCase(false)][TestCase(true)]
        public void UnchangedCurvedLoopReloadPreservesVisibleStateAndFutureTransport(bool seedHead)
        {
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            GameUpdater.RestoreCurrentTick(0);
            var world = services.GetRequiredService<BeltWorldDatastore>();
            var origin = new Vector3Int(20, 0, 30);
            var positions = new[] { origin, origin + Vector3Int.forward, origin + Vector3Int.forward + Vector3Int.right, origin + Vector3Int.right };
            var directions = new[] { BlockDirection.North, BlockDirection.East, BlockDirection.South, BlockDirection.West };
            var belts = new SegmentBeltComponent[4];
            for (int i = 3; 0 <= i; i--)
            {
                Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, positions[i], directions[i], Array.Empty<BlockCreateParam>(), out var block));
                belts[i] = block.GetComponent<SegmentBeltComponent>();
            }
            belts[seedHead ? 0 : 1].SetItem(0, ServerContext.ItemStackFactory.Create(new ItemId(7), 1));
            for (int tick = 0; tick < 3; tick++) GameUpdater.Update();
            var before = world.CaptureSnapshot();
            string save = services.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson();
            var (_, loaded) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ((WorldLoaderFromJson)loaded.GetRequiredService<IWorldSaveDataLoader>()).Load(save);
            var restoredWorld = loaded.GetRequiredService<BeltWorldDatastore>(); restoredWorld.Load();
            var after = restoredWorld.CaptureSnapshot();
            Assert.AreEqual(1, before.Routes.Length); Assert.AreEqual(1, after.Routes.Length);
            CollectionAssert.AreEqual(before.Routes[0].Cells, after.Routes[0].Cells, "unchanged curved path and deterministic cut");
            CollectionAssert.AreEqual(before.Routes[0].EntryCells, after.Routes[0].EntryCells, "head entry geometry");
            for (int i = 0; i < before.Simulation.Segments.Length; i++)
            {
                Assert.AreEqual(before.Simulation.Segments[i].PriorityIndex, after.Simulation.Segments[i].PriorityIndex);
                Assert.AreEqual(before.Simulation.Segments[i].BufferedItem, after.Simulation.Segments[i].BufferedItem);
            }
            if (seedHead) Assert.AreEqual(before.Simulation.Segments[0].Items[0].Item.AcceptedInput, after.Simulation.Segments[0].Items[0].Item.AcceptedInput);
            var original = new BeltReplaySimulation(before.Simulation);
            var restored = new BeltReplaySimulation(after.Simulation);
            var empty = new BeltReplayTick(Array.Empty<BeltReplaySpeedChange>(), Array.Empty<int>(), Array.Empty<int>(), Array.Empty<BeltReplayInsertion>());
            // 内部セルの入口正規化は描画と将来の搬送を変えず、周回後はheadの受入値も一致する。
            // Interior entry normalization must preserve rendering/future transport and converge after crossing the head.
            for (int tick = 0; tick <= 80; tick++)
            {
                if (tick % 4 == 0)
                {
                    var a = original.CaptureSnapshot(); var b = restored.CaptureSnapshot();
                    CollectionAssert.AreEqual(a.Segments.SelectMany(s => s.Items).Select(i => (i.Item.Guid, i.Item.ItemId, i.DistanceToExit)),
                        b.Segments.SelectMany(s => s.Items).Select(i => (i.Item.Guid, i.Item.ItemId, i.DistanceToExit)), $"ordered transport at tick {tick}");
                    Assert.AreEqual(1, a.Segments.Sum(s => s.Items.Length)); Assert.AreEqual(1, b.Segments.Sum(s => s.Items.Length));
                    using var drawA = new DrawFixture(before.Routes, a.Segments);
                    using var drawB = new DrawFixture(after.Routes, b.Segments);
                    Assert.That(Vector3.Distance(drawA.Positions().Single(), drawB.Positions().Single()), Is.LessThan(0.0001f), $"GPU position at tick {tick}");
                }
                if (tick == 80) break;
                original.ApplyTick(empty, false); restored.ApplyTick(empty, false);
            }
            Assert.AreEqual(original.ComputeStateHash(), restored.ComputeStateHash(), "full state converges after a complete lap");
        }
        private static void AssertPoint(BeltRoute route,int distance,Vector3 expected)
        {
            using var f = new DrawFixture(new[]{route},new[]{BeltReplaySegmentState.Normal(route.Cells.Length,16,new[]{Item(7,BeltDirection.Back,distance)})});
            Assert.That(Vector3.Distance(f.Positions()[0],expected),Is.LessThan(0.0001f));
        }
        internal static BeltRouteCell Cell(int x,int y,int height,int input) => new(new(x,y,0),BeltEntryDirection.FromBack,height,input);
        internal static BeltRouteCell[] Entries(BeltRouteCell cell) => Enumerable.Repeat(cell,4).ToArray();
        internal static BeltItemState Item(int kind,BeltDirection direction,int distance) => new(new BeltItem{Guid=Guid.NewGuid(),ItemId=kind,AcceptedInput=direction},distance);
    }
    internal sealed class DrawFixture : IDisposable
    {
        internal readonly GpuBeltSimulation Simulation;
        internal readonly BeltDrawBuffers Buffers;
        internal readonly BeltDrawDispatch Dispatch;
        internal DrawFixture(BeltRoute[] routes,BeltReplaySegmentState[] states)
        {
            var snapshot=new BeltReplaySnapshot(states,Array.Empty<BeltReplayLink>(),Array.Empty<BeltReplayInput>(),Array.Empty<BeltReplayOutput>());
            Simulation=new GpuBeltSimulation(snapshot,Resources.Load<ComputeShader>("BeltSegment/BeltGpuReplay"));
            int capacity=states.Sum(s=>s.Capacity);
            Buffers=new BeltDrawBuffers(new BeltDrawLayout(routes),capacity,new[]{7,900000},Resources.GetBuiltinResource<Mesh>("Cube.fbx"));
            Dispatch=new BeltDrawDispatch(Resources.Load<ComputeShader>("BeltSegment/Rendering/BeltItemDraw"),Simulation.Buffers,Buffers,capacity,routes.Length,2,new(0.5f,0.48f,0.5f));
            Dispatch.Recompute();
        }
        internal uint[] Counts(){var data=new uint[2];Buffers.Counts.GetData(data);return data;}
        internal Vector3[] Positions(){var data=new Vector4[Counts().Sum(v=>(int)v)];Buffers.Positions.GetData(data,0,0,data.Length);return data.Select(v=>(Vector3)v).ToArray();}
        public void Dispose(){Dispatch.Dispose();Buffers.Dispose();Simulation.Dispose();}
    }
}
