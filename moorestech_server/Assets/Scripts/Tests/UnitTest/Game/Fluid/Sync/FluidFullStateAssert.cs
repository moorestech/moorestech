using Game.Fluid.Sync.Replica;
using Game.Fluid.Sync.State;
using NUnit.Framework;

namespace Tests.UnitTest.Game.Fluid.Sync
{
    // 流体網の全量同士、または全量と複製を項目ごとに比べる。doubleはビット一致を求める(複製はサーバーと同じ計算を回すので丸めの差も許さない)
    // Field-by-field comparison of two fluid network full states, or of a full state and a replica; doubles must match bit for bit (the replica runs the same computation, so no rounding slack)
    public static class FluidFullStateAssert
    {
        // 複製は番号を持たないので、面とポートの相手ノードは全量側の番号が指すノードの座標で比べる
        // The replica carries no indices, so faces and ports are compared through the position of the node the full state's index points at
        public static void AreEqual(FluidNetworkFullState expected, FluidNetworkReplica replica)
        {
            Assert.AreEqual(expected.Nodes.Length, replica.Nodes.Length, "node count");
            for (var i = 0; i < expected.Nodes.Length; i++)
            {
                var e = expected.Nodes[i];
                var a = replica.Nodes[i];
                Assert.AreEqual(e.Position, a.Position, $"node {i} position");
                Assert.AreEqual(e.Capacity, a.Capacity, $"node {i} capacity");
                Assert.AreEqual(e.Amount, a.Amount, $"node {i} amount");
                Assert.AreEqual(e.FluidId, a.FluidId, $"node {i} fluid id");
            }

            Assert.AreEqual(expected.Faces.Length, replica.Faces.Length, "face count");
            for (var i = 0; i < expected.Faces.Length; i++)
            {
                var e = expected.Faces[i];
                var a = replica.Faces[i];
                Assert.AreEqual(expected.Nodes[e.NodeAIndex].Position, a.NodeA.Position, $"face {i} node A");
                Assert.AreEqual(expected.Nodes[e.NodeBIndex].Position, a.NodeB.Position, $"face {i} node B");
                Assert.AreEqual(e.FlowCapacityPerTick, a.FlowCapacityPerTick, $"face {i} flow capacity");
                Assert.AreEqual(e.Velocity, a.Velocity, $"face {i} velocity");
                Assert.AreEqual(e.AllowAToB, a.AllowAToB, $"face {i} allow A to B");
                Assert.AreEqual(e.AllowBToA, a.AllowBToA, $"face {i} allow B to A");
            }

            Assert.AreEqual(expected.Ports.Length, replica.Ports.Length, "port count");
            for (var i = 0; i < expected.Ports.Length; i++)
            {
                var e = expected.Ports[i];
                var a = replica.Ports[i];
                Assert.AreEqual(expected.Nodes[e.NodeIndex].Position, a.PipeNode.Position, $"port {i} node");
                Assert.AreEqual(e.FlowCapacityPerTick, a.FlowCapacityPerTick, $"port {i} flow capacity");
                Assert.AreEqual(e.Velocity, a.Velocity, $"port {i} velocity");
            }
        }

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

