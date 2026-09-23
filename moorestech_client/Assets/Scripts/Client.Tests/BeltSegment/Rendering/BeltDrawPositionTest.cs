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
            var route = new BeltRoute(Enumerable.Range(0,260).Select(i => Cell(0,i,0,0)).ToArray(), Entries(Cell(0,-1,0,0)));
            var items = Enumerable.Range(0,259).Select(i => Item(i % 2 == 0 ? 7 : 900000, BeltDirection.Back, i * 256)).ToArray();
            using var f = new DrawFixture(new[] { route }, new[] { BeltReplaySegmentState.Normal(260,16,items) });
            var state = new GpuBeltState[1]; f.Simulation.Buffers.States.GetData(state);
            var gaps = new int[260]; var values = new GpuBeltItem[260];
            for (int i = 0; i < 259; i++) values[(i+250)%260] = new GpuBeltItem { Kind = items[i].Item.ItemId, AcceptedInput = (int)BeltDirection.Back };
            state[0].Head = 250; f.Simulation.Buffers.States.SetData(state);
            f.Simulation.Buffers.Gaps.SetData(gaps); f.Simulation.Buffers.Items.SetData(values); f.Dispatch.Recompute();
            CollectionAssert.AreEqual(new uint[] {130,129}, f.Counts());
            var positions = f.Positions().OrderBy(v => v.z).ToArray();
            for(int i=0;i<259;i++) Assert.That(positions[i].z, Is.EqualTo(i+1.5f).Within(0.0001f));
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
