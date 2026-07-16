using Game.Block.Interface;
using Game.EnergySystem;
using NUnit.Framework;
using Tests.Util;

namespace Tests.UnitTest.Game
{
    // dirty境界の電力mapを検証する
    // Verifies that a dirty live graph does not leak into the applied map before the rebuild boundary
    public class ElectricWireNetworkDatastoreFlushTest
    {
        [SetUp]
        public void SetUp()
        {
            FakeWireConnector.ClearRegistry();
        }

        [Test]
        public void 追加は再構築まで適用済みmapへ反映されない()
        {
            var datastore = new ElectricWireNetworkDatastore();
            var connector = FakeWireConnector.CreateTransformer(1);

            datastore.AddConnector(connector);

            // 次境界まで旧mapを維持する
            // Update only the live registry and retain the applied map until the next boundary
            Assert.IsTrue(ElectricNetworkReflectionTestUtil.IsTopologyDirty(datastore));
            Assert.IsFalse(datastore.TryGetEnergySegment(connector.BlockInstanceId, out _));

            datastore.RebuildIfDirty();

            Assert.IsFalse(ElectricNetworkReflectionTestUtil.IsTopologyDirty(datastore));
            Assert.IsTrue(datastore.TryGetEnergySegment(connector.BlockInstanceId, out _));
            Assert.AreEqual(1, ElectricNetworkReflectionTestUtil.GetSegmentCount(datastore));
        }

        [Test]
        public void dirtyでない再構築は適用済みmapを維持する()
        {
            var datastore = new ElectricWireNetworkDatastore();
            datastore.AddConnector(FakeWireConnector.CreateTransformer(1));
            datastore.RebuildIfDirty();
            var appliedMap = ElectricNetworkReflectionTestUtil.GetTopologyMap(datastore);

            // clean時は同一mapを再利用する
            // Reuse the same applied map on a clean tick without rebuilding derived state
            datastore.RebuildIfDirty();

            Assert.AreSame(appliedMap, ElectricNetworkReflectionTestUtil.GetTopologyMap(datastore));
        }

        [Test]
        public void 同一境界内の変更は最終登録状態だけを再構築する()
        {
            var datastore = new ElectricWireNetworkDatastore();
            var removed = FakeWireConnector.CreateTransformer(1);
            var a = FakeWireConnector.CreateTransformer(2);
            var b = FakeWireConnector.CreateGenerator(3);

            datastore.AddConnector(removed);
            datastore.AddConnector(a);
            datastore.RemoveConnector(removed);
            datastore.AddConnector(b);
            FakeWireConnector.ConnectEachOther(a, b);
            datastore.MarkTopologyDirty();

            // 境界時点のlive graphを適用する
            // Apply only the registered vertices and edges present at the boundary, not mutation history
            datastore.RebuildIfDirty();

            Assert.IsFalse(datastore.TryGetEnergySegment(removed.BlockInstanceId, out _));
            Assert.IsTrue(datastore.TryGetEnergySegment(a.BlockInstanceId, out var segmentA));
            Assert.IsTrue(datastore.TryGetEnergySegment(b.BlockInstanceId, out var segmentB));
            Assert.AreSame(segmentA, segmentB);
            Assert.AreEqual(1, ElectricNetworkReflectionTestUtil.GetSegmentCount(datastore));
        }

        [Test]
        public void 一回の再構築は各登録頂点の隣接を一度だけ列挙する()
        {
            var datastore = new ElectricWireNetworkDatastore();
            var a = FakeWireConnector.CreateTransformer(1);
            var b = FakeWireConnector.CreateTransformer(2);
            var c = FakeWireConnector.CreateTransformer(3);
            var removed = FakeWireConnector.CreateTransformer(4);
            FakeWireConnector.ConnectEachOther(a, b);
            FakeWireConnector.ConnectEachOther(b, c);

            // 最終登録集合だけを走査する
            // Scan only the final registry once even when mutation history grows
            datastore.AddConnector(removed);
            datastore.RemoveConnector(removed);
            datastore.AddConnector(a);
            datastore.AddConnector(b);
            datastore.AddConnector(c);
            datastore.MarkTopologyDirty();
            datastore.MarkTopologyDirty();
            datastore.RebuildIfDirty();

            Assert.AreEqual(1, a.AdjacencyEnumerationCount);
            Assert.AreEqual(1, b.AdjacencyEnumerationCount);
            Assert.AreEqual(1, c.AdjacencyEnumerationCount);
            Assert.AreEqual(0, removed.AdjacencyEnumerationCount);
            Assert.AreEqual(4, a.YieldedConnectionCount + b.YieldedConnectionCount + c.YieldedConnectionCount);
        }

    }
}
