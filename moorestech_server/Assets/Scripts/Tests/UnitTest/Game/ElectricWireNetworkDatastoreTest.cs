using Game.Block.Interface;
using Game.EnergySystem;
using NUnit.Framework;

namespace Tests.UnitTest.Game
{
    /// <summary>
    /// ワイヤーグラフの連結成分管理（Add/Remove/Rebuild）を検証する。ワールド不要の純粋単体テスト
    /// Verifies connected-component management (Add/Remove/Rebuild) of the wire graph; a pure unit test without world bootstrap
    /// </summary>
    public class ElectricWireNetworkDatastoreTest
    {
        [Test]
        public void 孤立コネクタは単独セグメントに所属する()
        {
            var datastore = new ElectricWireNetworkDatastore();
            var connector = FakeWireConnector.CreateTransformer(1);
            datastore.AddConnector(connector);

            Assert.AreEqual(1, datastore.SegmentCount);
            Assert.IsTrue(datastore.TryGetEnergySegment(new BlockInstanceId(1), out var segment));
            Assert.IsTrue(segment.EnergyTransformers.ContainsKey(new BlockInstanceId(1)));
        }

        [Test]
        public void ワイヤー接続で2セグメントがマージされる()
        {
            var datastore = new ElectricWireNetworkDatastore();
            var a = FakeWireConnector.CreateTransformer(1);
            var b = FakeWireConnector.CreateGenerator(2);
            datastore.AddConnector(a);
            datastore.AddConnector(b);
            Assert.AreEqual(2, datastore.SegmentCount);

            FakeWireConnector.ConnectEachOther(a, b);
            datastore.RebuildAround(a, b);

            Assert.AreEqual(1, datastore.SegmentCount);
            datastore.TryGetEnergySegment(new BlockInstanceId(1), out var segA);
            datastore.TryGetEnergySegment(new BlockInstanceId(2), out var segB);
            Assert.AreSame(segA, segB);
        }

        [Test]
        public void ワイヤー切断でセグメントが分割される()
        {
            // A-B-C を接続後、A-B間を切断 → {A} と {B,C} の2セグメントになる
            // Connect A-B-C then cut A-B; expect two segments {A} and {B,C}
            var datastore = new ElectricWireNetworkDatastore();
            var a = FakeWireConnector.CreateTransformer(1);
            var b = FakeWireConnector.CreateTransformer(2);
            var c = FakeWireConnector.CreateTransformer(3);
            datastore.AddConnector(a);
            datastore.AddConnector(b);
            datastore.AddConnector(c);

            FakeWireConnector.ConnectEachOther(a, b);
            FakeWireConnector.ConnectEachOther(b, c);
            datastore.RebuildAround(a, b, c);
            Assert.AreEqual(1, datastore.SegmentCount);

            FakeWireConnector.DisconnectEachOther(a, b);
            datastore.RebuildAround(a, b);

            Assert.AreEqual(2, datastore.SegmentCount);
            datastore.TryGetEnergySegment(new BlockInstanceId(1), out var segA);
            datastore.TryGetEnergySegment(new BlockInstanceId(2), out var segB);
            datastore.TryGetEnergySegment(new BlockInstanceId(3), out var segC);
            Assert.AreNotSame(segA, segB);
            Assert.AreSame(segB, segC);
        }

        [Test]
        public void コネクタ除去で残りが再構成される()
        {
            // A-B-C（Bが中央）でBをRemoveConnector → AとCが別セグメントに分かれる
            // Connect A-B-C (B in the middle) then RemoveConnector(B); expect A and C to end up in separate segments
            var datastore = new ElectricWireNetworkDatastore();
            var a = FakeWireConnector.CreateGenerator(1);
            var b = FakeWireConnector.CreateTransformer(2);
            var c = FakeWireConnector.CreateConsumer(3);
            datastore.AddConnector(a);
            datastore.AddConnector(b);
            datastore.AddConnector(c);

            FakeWireConnector.ConnectEachOther(a, b);
            FakeWireConnector.ConnectEachOther(b, c);
            datastore.RebuildAround(a, b, c);
            Assert.AreEqual(1, datastore.SegmentCount);

            datastore.RemoveConnector(b);

            Assert.AreEqual(2, datastore.SegmentCount);
            Assert.IsTrue(datastore.TryGetEnergySegment(new BlockInstanceId(1), out var segA));
            Assert.IsTrue(datastore.TryGetEnergySegment(new BlockInstanceId(3), out var segC));
            Assert.AreNotSame(segA, segC);
            Assert.IsFalse(datastore.TryGetEnergySegment(new BlockInstanceId(2), out _));
        }
    }
}
