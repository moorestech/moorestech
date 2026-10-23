using System;
using System.Text.RegularExpressions;
using Core.Master;
using Game.Fluid.Sync.Diff;
using Game.Fluid.Sync.Replica;
using Game.Fluid.Sync.State;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.Fluid.Sync
{
    // 全量から組んだ複製が全量と同じ項目を持ち、差分の再生が境界の出入りを足し、再現できない差分を食い違いとして報告するか
    // Whether a replica assembled from a full state holds the same fields, diff replay adds the boundary exchange, and an irreproducible diff is reported as a divergence
    public class FluidNetworkReplicaTest
    {
        private static readonly FluidId Water = new(1);
        private static readonly FluidId Steam = new(2);

        [Test]
        public void 全量から組んだ複製は全量と同じ項目を持つ()
        {
            var full = TwoNodesWithOnePort();
            var replica = FluidNetworkReplicaAssembler.Assemble(full);

            FluidFullStateAssert.AreEqual(full, replica);
            Assert.IsTrue(replica.TryGetNodeIndex(new Vector3Int(0, 0, 1), out var node));
            Assert.AreEqual(30, replica.Network.NodeAmounts[node]);
        }

        [Test]
        public void 流入の再生は受け入れ済み量をそのまま足し空のノードは流体IDを引き継ぐ()
        {
            var replica = FluidNetworkReplicaAssembler.Assemble(TwoNodesWithOnePort());
            var inflows = new[] { new FluidNodeInflowRecord(1, Water, 5), new FluidNodeInflowRecord(0, Water, 7) };

            Assert.IsTrue(replica.Tick(new FluidTickDiff(Array.Empty<FluidPortDeliveryRecord>(), inflows)));

            // 空だったノード0は水になり7を持つ。ノード1は面の流れを除けば5増える(面は許可方向が無いので流れない)
            // Node 0, previously empty, becomes water holding 7; node 1 gains 5 (the face allows no direction, so nothing flows)
            Assert.AreEqual(Water, replica.Network.NodeFluidIds[0]);
            Assert.AreEqual(7, replica.Network.NodeAmounts[0], 1e-9);
            Assert.AreEqual(35, replica.Network.NodeAmounts[1], 1e-9);
        }

        [Test]
        public void 中身のあるノードへの異種流体の流入は食い違いになる()
        {
            var replica = FluidNetworkReplicaAssembler.Assemble(TwoNodesWithOnePort());
            LogAssert.Expect(LogType.Error, new Regex("holds fluid"));

            Assert.IsFalse(replica.Tick(new FluidTickDiff(Array.Empty<FluidPortDeliveryRecord>(), new[] { new FluidNodeInflowRecord(1, Steam, 1) })));
        }

        [Test]
        public void 範囲外のポート番号への渡しは食い違いになる()
        {
            var replica = FluidNetworkReplicaAssembler.Assemble(TwoNodesWithOnePort());
            LogAssert.Expect(LogType.Error, new Regex("delivery for port"));

            Assert.IsFalse(replica.Tick(new FluidTickDiff(new[] { new FluidPortDeliveryRecord(5, 1) }, Array.Empty<FluidNodeInflowRecord>())));
        }

        [Test]
        public void 予告された渡しは複製のポートが同じ量を機械へ渡したものとして減る()
        {
            // ノード1は水30でポートに繋がる。ポート速度を十分に立てておき、予告量2だけが機械に受け取られる
            // Node 1 holds 30 water and feeds the port; the port velocity is already high, and only the announced 2 is accepted by the machine
            var full = new FluidNetworkFullState(
                new[] { new FluidNodeState(new Vector3Int(0, 0, 0), 100, 0, FluidMaster.EmptyFluidId), new FluidNodeState(new Vector3Int(0, 0, 1), 100, 30, Water) },
                Array.Empty<FluidFaceState>(),
                new[] { new FluidPortState(1, 10, 10) });
            var replica = FluidNetworkReplicaAssembler.Assemble(full);

            Assert.IsTrue(replica.Tick(new FluidTickDiff(new[] { new FluidPortDeliveryRecord(0, 2) }, Array.Empty<FluidNodeInflowRecord>())));

            Assert.AreEqual(28, replica.Network.NodeAmounts[1], 1e-9);
            Assert.AreEqual(2, replica.Network.Ports[0].Velocity, 1e-9, "the port velocity converges onto the settled amount");
        }

        private static FluidNetworkFullState TwoNodesWithOnePort()
        {
            return new FluidNetworkFullState(
                new[] { new FluidNodeState(new Vector3Int(0, 0, 0), 100, 0, FluidMaster.EmptyFluidId), new FluidNodeState(new Vector3Int(0, 0, 1), 100, 30, Water) },
                new[] { new FluidFaceState(0, 1, 5, 0, false, false) },
                new[] { new FluidPortState(1, 10, 0) });
        }
    }
}

