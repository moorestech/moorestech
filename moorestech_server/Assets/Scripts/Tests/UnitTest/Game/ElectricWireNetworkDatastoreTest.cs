using Game.Block.Interface;
using Game.EnergySystem;
using NUnit.Framework;
using Tests.Util;

namespace Tests.UnitTest.Game
{
    // live電線から最終所属を検証する
    // Verifies final connected components and role membership rebuilt from the live wire graph
    public class ElectricWireNetworkDatastoreTest
    {
        [SetUp]
        public void SetUp()
        {
            FakeWireConnector.ClearRegistry();
        }

        [Test]
        public void 孤立コネクタは単独セグメントに所属する()
        {
            var datastore = new ElectricWireNetworkDatastore();
            var connector = FakeWireConnector.CreateTransformer(1);
            datastore.AddConnector(connector);
            datastore.RebuildIfDirty();

            Assert.AreEqual(1, ElectricNetworkReflectionTestUtil.GetSegmentCount(datastore));
            Assert.IsTrue(datastore.TryGetEnergySegment(connector.BlockInstanceId, out _));
        }

        [Test]
        public void ワイヤー接続で2セグメントがマージされる()
        {
            var datastore = new ElectricWireNetworkDatastore();
            var a = FakeWireConnector.CreateTransformer(1);
            var b = FakeWireConnector.CreateGenerator(2);
            datastore.AddConnector(a);
            datastore.AddConnector(b);
            datastore.RebuildIfDirty();
            Assert.AreEqual(2, ElectricNetworkReflectionTestUtil.GetSegmentCount(datastore));

            FakeWireConnector.ConnectEachOther(a, b);
            datastore.MarkTopologyDirty();
            datastore.RebuildIfDirty();

            Assert.AreEqual(1, ElectricNetworkReflectionTestUtil.GetSegmentCount(datastore));
            datastore.TryGetEnergySegment(a.BlockInstanceId, out var segmentA);
            datastore.TryGetEnergySegment(b.BlockInstanceId, out var segmentB);
            Assert.AreSame(segmentA, segmentB);
        }

        [Test]
        public void ワイヤー切断でセグメントが分割される()
        {
            var datastore = new ElectricWireNetworkDatastore();
            var a = FakeWireConnector.CreateTransformer(1);
            var b = FakeWireConnector.CreateTransformer(2);
            var c = FakeWireConnector.CreateTransformer(3);
            datastore.AddConnector(a);
            datastore.AddConnector(b);
            datastore.AddConnector(c);
            FakeWireConnector.ConnectEachOther(a, b);
            FakeWireConnector.ConnectEachOther(b, c);
            datastore.RebuildIfDirty();
            Assert.AreEqual(1, ElectricNetworkReflectionTestUtil.GetSegmentCount(datastore));

            FakeWireConnector.DisconnectEachOther(a, b);
            datastore.MarkTopologyDirty();
            datastore.RebuildIfDirty();

            Assert.AreEqual(2, ElectricNetworkReflectionTestUtil.GetSegmentCount(datastore));
            datastore.TryGetEnergySegment(a.BlockInstanceId, out var segmentA);
            datastore.TryGetEnergySegment(b.BlockInstanceId, out var segmentB);
            datastore.TryGetEnergySegment(c.BlockInstanceId, out var segmentC);
            Assert.AreNotSame(segmentA, segmentB);
            Assert.AreSame(segmentB, segmentC);
        }

        [Test]
        public void 複数セグメントの橋渡し後は最終所属だけが一致する()
        {
            var datastore = new ElectricWireNetworkDatastore();
            var a = FakeWireConnector.CreateTransformer(1);
            var b = FakeWireConnector.CreateTransformer(2);
            var c = FakeWireConnector.CreateGenerator(3);
            var d = FakeWireConnector.CreateConsumer(4);
            datastore.AddConnector(a);
            datastore.AddConnector(b);
            datastore.AddConnector(c);
            datastore.AddConnector(d);
            FakeWireConnector.ConnectEachOther(a, b);
            FakeWireConnector.ConnectEachOther(c, d);
            datastore.RebuildIfDirty();
            Assert.AreEqual(2, ElectricNetworkReflectionTestUtil.GetSegmentCount(datastore));

            // full rebuildは最終接続だけを見る
            // Full rebuild treats only final connectivity as truth and does not preserve old segment identity
            FakeWireConnector.ConnectEachOther(b, c);
            datastore.MarkTopologyDirty();
            datastore.RebuildIfDirty();

            Assert.AreEqual(1, ElectricNetworkReflectionTestUtil.GetSegmentCount(datastore));
            datastore.TryGetEnergySegment(a.BlockInstanceId, out var segment);
            Assert.IsTrue(datastore.TryGetEnergySegment(b.BlockInstanceId, out var segmentB));
            Assert.IsTrue(datastore.TryGetEnergySegment(c.BlockInstanceId, out var segmentC));
            Assert.IsTrue(datastore.TryGetEnergySegment(d.BlockInstanceId, out var segmentD));
            Assert.AreSame(segment, segmentB);
            Assert.AreSame(segment, segmentC);
            Assert.AreSame(segment, segmentD);
            Assert.AreEqual(1, ElectricNetworkReflectionTestUtil.GetGenerators(segment).Count);
            Assert.AreEqual(1, ElectricNetworkReflectionTestUtil.GetConsumers(segment).Count);
        }

        [Test]
        public void GetSegmentsは現存する全セグメントを返す()
        {
            var datastore = new ElectricWireNetworkDatastore();
            var a = FakeWireConnector.CreateTransformer(1);
            var b = FakeWireConnector.CreateGenerator(2);
            datastore.AddConnector(a);
            datastore.AddConnector(b);
            datastore.RebuildIfDirty();

            var segments = datastore.GetSegments();
            datastore.TryGetEnergySegment(a.BlockInstanceId, out var segmentA);
            datastore.TryGetEnergySegment(b.BlockInstanceId, out var segmentB);
            Assert.AreEqual(2, segments.Count);
            CollectionAssert.Contains(segments, segmentA);
            CollectionAssert.Contains(segments, segmentB);

            FakeWireConnector.ConnectEachOther(a, b);
            datastore.MarkTopologyDirty();
            datastore.RebuildIfDirty();
            Assert.AreEqual(1, datastore.GetSegments().Count);
        }

        [Test]
        public void コネクタ除去で残りが再構成される()
        {
            var datastore = new ElectricWireNetworkDatastore();
            var a = FakeWireConnector.CreateGenerator(1);
            var b = FakeWireConnector.CreateTransformer(2);
            var c = FakeWireConnector.CreateConsumer(3);
            datastore.AddConnector(a);
            datastore.AddConnector(b);
            datastore.AddConnector(c);
            FakeWireConnector.ConnectEachOther(a, b);
            FakeWireConnector.ConnectEachOther(b, c);
            datastore.RebuildIfDirty();

            datastore.RemoveConnector(b);
            datastore.RebuildIfDirty();

            Assert.AreEqual(2, ElectricNetworkReflectionTestUtil.GetSegmentCount(datastore));
            Assert.IsTrue(datastore.TryGetEnergySegment(a.BlockInstanceId, out var segmentA));
            Assert.IsTrue(datastore.TryGetEnergySegment(c.BlockInstanceId, out var segmentC));
            Assert.AreNotSame(segmentA, segmentC);
            Assert.IsFalse(datastore.TryGetEnergySegment(new BlockInstanceId(2), out _));
        }
    }
}
