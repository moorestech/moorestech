using Game.Block.Interface;
using Game.EnergySystem;
using NUnit.Framework;

namespace Tests.UnitTest.Game
{
    /// <summary>
    /// トポロジ変更がコマンドとして保留され、電力tick先頭のflush（ElectricTickUpdater.Update）まで適用されないことを検証する
    /// Verifies that topology mutations stay queued as commands and are not applied until the electric tick head flush (ElectricTickUpdater.Update)
    /// </summary>
    public class ElectricWireNetworkDatastoreFlushTest
    {
        [SetUp]
        public void SetUp()
        {
            FakeWireConnector.ClearRegistry();
        }

        [Test]
        public void 追加コマンドはflushまで反映されない()
        {
            var datastore = new ElectricWireNetworkDatastore();
            var connector = FakeWireConnector.CreateTransformer(1);

            datastore.AddConnector(connector);

            // flush前はセグメントが存在しない
            // No segment exists before the flush
            Assert.AreEqual(0, datastore.SegmentCount);
            Assert.IsFalse(datastore.TryGetEnergySegment(new BlockInstanceId(1), out _));

            new ElectricTickUpdater(datastore).Update();

            Assert.AreEqual(1, datastore.SegmentCount);
            Assert.IsTrue(datastore.TryGetEnergySegment(new BlockInstanceId(1), out _));
        }

        [Test]
        public void 削除コマンドはflushまで反映されない()
        {
            var datastore = new ElectricWireNetworkDatastore();
            var connector = FakeWireConnector.CreateTransformer(1);
            datastore.AddConnector(connector);
            new ElectricTickUpdater(datastore).Update();
            Assert.AreEqual(1, datastore.SegmentCount);

            datastore.RemoveConnector(connector);

            // flush前は旧セグメントが維持され、所属も変化しない
            // The old segment and its membership stay intact before the flush
            Assert.AreEqual(1, datastore.SegmentCount);
            Assert.IsTrue(datastore.TryGetEnergySegment(new BlockInstanceId(1), out _));

            new ElectricTickUpdater(datastore).Update();

            Assert.AreEqual(0, datastore.SegmentCount);
            Assert.IsFalse(datastore.TryGetEnergySegment(new BlockInstanceId(1), out _));
        }

        [Test]
        public void 同一tick内の追加と削除はFIFOで相殺される()
        {
            var datastore = new ElectricWireNetworkDatastore();
            var connector = FakeWireConnector.CreateTransformer(1);

            // 追加→削除を同一バッチに積むと、flush後は何も残らない
            // Queueing add then remove in one batch leaves nothing after the flush
            datastore.AddConnector(connector);
            datastore.RemoveConnector(connector);
            new ElectricTickUpdater(datastore).Update();

            Assert.AreEqual(0, datastore.SegmentCount);
            Assert.IsFalse(datastore.TryGetEnergySegment(new BlockInstanceId(1), out _));
        }
    }
}
