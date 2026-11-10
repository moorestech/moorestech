using System.Collections.Generic;
using Client.Game.InGame.FluidNetwork;
using Core.Master;
using Game.Fluid;
using Game.Fluid.Sync.Diff;
using NUnit.Framework;
using UniRx;
using static Tests.CombinedTest.Server.PacketTest.Event.FluidNetworkSync.FluidSyncPacketTestUtil;

namespace Client.Tests.FluidNetwork
{
    // 適用のたび、版を進めた後に通知が 1 回流れるか
    // Each apply emits one version notification after advancing
    public class FluidNetworkClientCacheNotificationTest
    {
        private const long InflowUnits = 5 * FluidUnits.UnitsPerAmount;

        [Test]
        public void 全量の適用と差分の適用のたびに版の通知が1回流れ_購読の中の複製は進んだ後の値()
        {
            CreateServer();
            BuildPumpFedWorldAndWarmUp();
            var cache = new FluidNetworkClientCache();
            var notified = new List<int>();
            var amountsSeenInside = new List<long>();
            var nodeIndex = -1;
            cache.OnReplicaVersionChanged.Subscribe(version =>
            {
                notified.Add(version);
                if (0 <= nodeIndex) amountsSeenInside.Add(cache.Replica.Network.NodeAmounts[nodeIndex]);
            });

            var fullState = CaptureServer();
            cache.ApplyFullState(fullState);
            Assert.AreEqual(1, notified.Count);
            Assert.AreEqual(cache.ReplicaVersion, notified[0]);

            // 流入 1 つの差分は双子に足した量
            // A one-inflow diff equals the twin plus the inflow
            nodeIndex = FindFilledNode(cache);
            var fluidId = cache.Replica.Network.NodeFluidIds[nodeIndex];
            var twin = new FluidNetworkClientCache();
            twin.ApplyFullState(fullState);
            twin.ApplyTickDiff(FluidTickDiff.Empty);
            var expected = twin.Replica.Network.NodeAmounts[nodeIndex] + InflowUnits;
            cache.ApplyTickDiff(new FluidTickDiff(new FluidPortDeliveryRecord[0], new[] { new FluidNodeInflowRecord(nodeIndex, fluidId, InflowUnits) }));

            Assert.AreEqual(2, notified.Count);
            Assert.AreEqual(cache.ReplicaVersion, notified[1]);
            Assert.AreEqual(1, amountsSeenInside.Count);
            Assert.AreEqual(expected, amountsSeenInside[0], "購読の中で読んだ量が差分を当てた後の値");
        }

        private static int FindFilledNode(FluidNetworkClientCache cache)
        {
            var network = cache.Replica.Network;
            for (var i = 0; i < network.NodeCount; i++)
            {
                if (0 < network.NodeAmounts[i]) return i;
            }
            Assert.Fail("filled node not found");
            return -1;
        }
    }
}
