using Core.Master;
using Game.Fluid;
using Game.Fluid.Simulation;
using Game.Fluid.Sync.Replica;
using Game.Fluid.Sync.State;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.Fluid.Sync
{
    // 流体ハッシュが tick で変わる項目(量・流体ID・面とポートの速度)と件数のどれを欠いても検知でき、増分更新が全走査と一致するか
    // Whether the fluid hash notices a change in any per-tick field (amount, fluid id, face and port velocity) or count, and incremental updates agree with the full walk
    public class FluidNetworkStateHashTest
    {
        private static readonly FluidId Water = new(1);
        private static readonly FluidId Steam = new(2);

        [Test]
        public void 量を変えるとハッシュが変わる()
        {
            var network = Build(2);
            var before = FluidNetworkStateHash.Compute(network);

            network.NodeAmounts[1] += 1;

            Assert.AreNotEqual(before, FluidNetworkStateHash.Compute(network));
        }

        [Test]
        public void 流体IDを変えるとハッシュが変わる()
        {
            var network = Build(2);
            var before = FluidNetworkStateHash.Compute(network);

            network.NodeFluidIds[1] = Steam;

            Assert.AreNotEqual(before, FluidNetworkStateHash.Compute(network));
        }

        [Test]
        public void 面の速度を変えるとハッシュが変わる()
        {
            var network = Build(2);
            var before = FluidNetworkStateHash.Compute(network);

            network.FaceVelocities[0] += 1;

            Assert.AreNotEqual(before, FluidNetworkStateHash.Compute(network));
        }

        [Test]
        public void ポートの速度を変えるとハッシュが変わる()
        {
            var network = Build(2);
            var before = FluidNetworkStateHash.Compute(network);

            network.Ports[0].Velocity += 1;

            Assert.AreNotEqual(before, FluidNetworkStateHash.Compute(network));
        }

        [Test]
        public void ノード数が違うとハッシュが変わる()
        {
            Assert.AreNotEqual(FluidNetworkStateHash.Compute(Build(2)), FluidNetworkStateHash.Compute(Build(3)));
        }

        [Test]
        public void 境界からの流入の後も網のハッシュは全走査と一致する()
        {
            var network = Build(2);

            // 空のノードへ別の流体を足す(流体IDの引き継ぎ)、満たされたノードへ足す、複製の差分再生の足し込みの順に確かめる
            // Check in order: adding to an empty node (fluid id adoption), adding to a filled node, and the replica's diff-replay addition
            network.AddExternal(0, new FluidStack(10, Steam), out _);
            Assert.AreEqual(FluidNetworkStateHash.Compute(network), network.LastStateHash, "AddExternal into an empty node");

            network.AddExternal(1, new FluidStack(5, Water), out _);
            Assert.AreEqual(FluidNetworkStateHash.Compute(network), network.LastStateHash, "AddExternal into a filled node");

            network.AddAccepted(1, Water, 3);
            Assert.AreEqual(FluidNetworkStateHash.Compute(network), network.LastStateHash, "AddAccepted");
        }

        [Test]
        public void 機械の流入はほぼ満杯のノードでも受け入れた量だけを網と記録に与える()
        {
            // 空き 200 単位(0.2)のノードへ空きの2倍を超える量を押し込む。網へ足された単位と out accepted は同じ値で、残りは double で機械へ返る
            // Push more than twice the free space into a node with 200 units (0.2) free; the units added to the network and the out accepted are the same value and the remainder returns to the machine as a double
            var network = Build(2);
            var capacity = network.NodeCapacities[1];
            network.NodeAmounts[1] = capacity - 200;
            var before = network.NodeAmounts[1];

            var remainder = network.AddExternal(1, new FluidStack(0.55, Water), out var accepted);

            Assert.AreEqual(200, accepted);
            Assert.AreEqual(network.NodeAmounts[1] - before, accepted);
            Assert.AreEqual(0.55 - 0.2, remainder.Amount, 1e-12);
        }

        private static FluidSimNetwork Build(int nodeCount)
        {
            var nodes = new FluidNodeState[nodeCount];
            for (var i = 0; i < nodeCount; i++) nodes[i] = new FluidNodeState(new Vector3Int(0, 0, i), 100, i == 1 ? 30 : 0, i == 1 ? Water : FluidMaster.EmptyFluidId);
            var full = new FluidNetworkFullState(
                nodes,
                new[] { new FluidFaceState(0, 1, 5, 2, true, true) },
                new[] { new FluidPortState(1, 10, 3) });
            return FluidNetworkReplicaAssembler.Assemble(full).Network;
        }
    }
}
