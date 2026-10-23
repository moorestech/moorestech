using Game.Fluid.Simulation;
using Game.Fluid.Sync.Replica;
using Game.Fluid.Sync.State;
using NUnit.Framework;

namespace Tests.UnitTest.Game.Fluid.Sync
{
    // 流体網の全量同士、または全量と網(複製)を項目ごとに比べる。doubleはビット一致を求める(複製はサーバーと同じ計算を回すので丸めの差も許さない)
    // Field-by-field comparison of two fluid network full states, or of a full state and a network (replica); doubles must match bit for bit (the replica runs the same computation, so no rounding slack)
    public static class FluidFullStateAssert
    {
        public static void AreEqual(FluidNetworkFullState expected, FluidNetworkReplica replica)
        {
            AreEqual(expected, replica.Network);
        }

        public static void AreEqual(FluidNetworkFullState expected, FluidSimNetwork network)
        {
            Assert.AreEqual(expected.Nodes.Length, network.NodeCount, "node count");
            for (var i = 0; i < expected.Nodes.Length; i++)
            {
                var e = expected.Nodes[i];
                Assert.AreEqual(e.Position, network.NodePositions[i], $"node {i} position");
                Assert.AreEqual(e.Capacity, network.NodeCapacities[i], $"node {i} capacity");
                Assert.AreEqual(e.Amount, network.NodeAmounts[i], $"node {i} amount");
                Assert.AreEqual(e.FluidId, network.NodeFluidIds[i], $"node {i} fluid id");
            }

            Assert.AreEqual(expected.Faces.Length, network.FaceCount, "face count");
            for (var i = 0; i < expected.Faces.Length; i++)
            {
                var e = expected.Faces[i];
                Assert.AreEqual(e.NodeAIndex, network.FaceNodeA[i], $"face {i} node A");
                Assert.AreEqual(e.NodeBIndex, network.FaceNodeB[i], $"face {i} node B");
                Assert.AreEqual(e.FlowCapacityPerTick, network.FaceFlowCapacities[i], $"face {i} flow capacity");
                Assert.AreEqual(e.Velocity, network.FaceVelocities[i], $"face {i} velocity");
                Assert.AreEqual(e.AllowAToB, network.FaceAllowAToB[i], $"face {i} allow A to B");
                Assert.AreEqual(e.AllowBToA, network.FaceAllowBToA[i], $"face {i} allow B to A");
            }

            Assert.AreEqual(expected.Ports.Length, network.Ports.Length, "port count");
            for (var i = 0; i < expected.Ports.Length; i++)
            {
                var e = expected.Ports[i];
                var a = network.Ports[i];
                Assert.AreEqual(e.NodeIndex, a.NodeIndex, $"port {i} node");
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
