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
        // 前テストのIDが残ると別テストを偽成功させ得るため、毎回レジストリを空にする
        // Clear the registry each run since a leftover ID from a prior test could cause a false pass
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
        public void 複数メンバーセグメント同士の橋渡しでUnionBySizeマージされる()
        {
            // {A,B}と{C,D}の2網を作りB-Cで橋渡し → AddConnector(C)時に2セグメントが見えMergeSegmentsが走る
            // Build nets {A,B} and {C,D}, then bridge B-C; AddConnector(C) sees two segments and runs MergeSegments
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
            datastore.RebuildAround(a, b);
            FakeWireConnector.ConnectEachOther(c, d);
            datastore.RebuildAround(c, d);
            Assert.AreEqual(2, datastore.SegmentCount);

            // 2メンバーの{A,B}側が吸収側になることを参照同一性で検証するため、橋渡し前に控えておく
            // Capture the 2-member {A,B} segment beforehand to verify by reference identity that it absorbs the other
            datastore.TryGetEnergySegment(new BlockInstanceId(1), out var segmentAbBefore);

            FakeWireConnector.ConnectEachOther(b, c);
            datastore.RebuildAround(b, c);

            // 全員が同一セグメントに統合され、吸収側はサイズの大きい{A,B}側
            // Everyone is folded into one segment, and the larger {A,B} side is the absorber
            Assert.AreEqual(1, datastore.SegmentCount);
            datastore.TryGetEnergySegment(new BlockInstanceId(1), out var segA);
            datastore.TryGetEnergySegment(new BlockInstanceId(2), out var segB);
            datastore.TryGetEnergySegment(new BlockInstanceId(3), out var segC);
            datastore.TryGetEnergySegment(new BlockInstanceId(4), out var segD);
            Assert.AreSame(segA, segB);
            Assert.AreSame(segA, segC);
            Assert.AreSame(segA, segD);
            Assert.AreSame(segmentAbBefore, segA);

            // 統合先セグメントに全役割が揃っている
            // The surviving segment holds every role from both nets
            Assert.IsTrue(segA.EnergyTransformers.ContainsKey(new BlockInstanceId(1)));
            Assert.IsTrue(segA.EnergyTransformers.ContainsKey(new BlockInstanceId(2)));
            Assert.IsTrue(segA.Generators.ContainsKey(new BlockInstanceId(3)));
            Assert.IsTrue(segA.Consumers.ContainsKey(new BlockInstanceId(4)));
        }

        [Test]
        public void GetSegmentsは現存する全セグメントを返す()
        {
            var datastore = new ElectricWireNetworkDatastore();
            var a = FakeWireConnector.CreateTransformer(1);
            var b = FakeWireConnector.CreateGenerator(2);
            datastore.AddConnector(a);
            datastore.AddConnector(b);

            // 孤立2コネクタ時点では2セグメントがそれぞれ返る
            // With two isolated connectors, both segments are returned
            var segments = datastore.GetSegments();
            datastore.TryGetEnergySegment(new BlockInstanceId(1), out var segA);
            datastore.TryGetEnergySegment(new BlockInstanceId(2), out var segB);
            Assert.AreEqual(2, segments.Count);
            CollectionAssert.Contains(segments, segA);
            CollectionAssert.Contains(segments, segB);

            // マージ後は統合された1セグメントだけが返る
            // After the merge, only the single unified segment is returned
            FakeWireConnector.ConnectEachOther(a, b);
            datastore.RebuildAround(a, b);
            var mergedSegments = datastore.GetSegments();
            datastore.TryGetEnergySegment(new BlockInstanceId(1), out var merged);
            Assert.AreEqual(1, mergedSegments.Count);
            CollectionAssert.Contains(mergedSegments, merged);
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
