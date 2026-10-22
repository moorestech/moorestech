using Game.Fluid.Sync.State;
using NUnit.Framework;

namespace Tests.UnitTest.Game.Fluid.Sync
{
    // 流体網の全量同士を項目ごとに比べる。doubleはビット一致を求める(複製はサーバーと同じ計算を回すので丸めの差も許さない)
    // Field-by-field comparison of two fluid network full states; doubles must match bit for bit (the replica runs the same computation, so no rounding slack)
    public static class FluidFullStateAssert
    {
        public static void AreEqual(FluidNetworkFullState expected, FluidNetworkFullState actual)
        {
            Assert.AreEqual(expected.Nodes.Length, actual.Nodes.Length, "node count");
            for (var i = 0; i < expected.Nodes.Length; i++)
            {
                var e = expected.Nodes[i];
                var a = actual.Nodes[i];
                Assert.AreEqual(e.Position, a.Position, $"node {i} position");
                Assert.AreEqual(e.Capacity, a.Capacity, $"node {i} capacity");
                Assert.AreEqual(e.Amount, a.Amount, $"node {i} amount");
                Assert.AreEqual(e.FluidId, a.FluidId, $"node {i} fluid id");
            }

            Assert.AreEqual(expected.Faces.Length, actual.Faces.Length, "face count");
            for (var i = 0; i < expected.Faces.Length; i++)
            {
                var e = expected.Faces[i];
                var a = actual.Faces[i];
                Assert.AreEqual(e.NodeAIndex, a.NodeAIndex, $"face {i} node A");
                Assert.AreEqual(e.NodeBIndex, a.NodeBIndex, $"face {i} node B");
                Assert.AreEqual(e.FlowCapacityPerTick, a.FlowCapacityPerTick, $"face {i} flow capacity");
                Assert.AreEqual(e.Velocity, a.Velocity, $"face {i} velocity");
                Assert.AreEqual(e.AllowAToB, a.AllowAToB, $"face {i} allow A to B");
                Assert.AreEqual(e.AllowBToA, a.AllowBToA, $"face {i} allow B to A");
            }

            Assert.AreEqual(expected.Ports.Length, actual.Ports.Length, "port count");
            for (var i = 0; i < expected.Ports.Length; i++)
            {
                var e = expected.Ports[i];
                var a = actual.Ports[i];
                Assert.AreEqual(e.NodeIndex, a.NodeIndex, $"port {i} node");
                Assert.AreEqual(e.FlowCapacityPerTick, a.FlowCapacityPerTick, $"port {i} flow capacity");
                Assert.AreEqual(e.Velocity, a.Velocity, $"port {i} velocity");
            }
        }
    }
}

