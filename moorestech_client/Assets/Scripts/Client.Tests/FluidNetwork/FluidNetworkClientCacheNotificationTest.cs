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
    // 全量の適用と差分の適用のたびに、複製と版を進め終えてから版の通知が 1 回流れるか
    // Whether each full-state and tick-diff application emits one version notification after the replica and version have advanced
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

            // 中身のある節へ流入を 1 つ載せた差分。購読の中で読んだ量は、同じ全量から空の差分で進めた双子の量に流入を足した値と一致する
            // A diff carrying one inflow into a filled node; the amount read inside the subscription equals a twin advanced by an empty diff from the same full state plus the inflow
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
