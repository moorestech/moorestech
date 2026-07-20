using System.Collections.Generic;
using Game.Block.Interface;
using Game.Gear.Common;
using NUnit.Framework;
using Tests.Util;

namespace Tests.UnitTest.Game.GearTopology
{
    // gear full rebuildの計算量境界を純粋単体テストで検証する
    // Verifies the gear full-rebuild complexity bound in pure unit tests
    public class GearNetworkTestDirtyRebuild
    {
        [Test]
        public void 一回の再構築は各登録gearの隣接を一度だけ列挙する()
        {
            var datastore = new GearNetworkDatastore();
            var a = new FakeGear(1);
            var b = new FakeGear(2);
            var c = new FakeGear(3);
            var removed = new FakeGear(4);
            FakeGear.ConnectEachOther(a, b);
            FakeGear.ConnectEachOther(b, c);

            // add/remove履歴ではなく境界時点のlive登録だけをBFS対象にする
            // Run BFS over only the live registry at the boundary, not add/remove history
            GearNetworkDatastore.AddGear(removed);
            GearNetworkDatastore.RemoveGear(removed);
            GearNetworkDatastore.AddGear(a);
            GearNetworkDatastore.AddGear(b);
            GearNetworkDatastore.AddGear(c);
            datastore.RebuildIfDirty();

            Assert.AreEqual(1, a.AdjacencyEnumerationCount);
            Assert.AreEqual(1, b.AdjacencyEnumerationCount);
            Assert.AreEqual(1, c.AdjacencyEnumerationCount);
            Assert.AreEqual(0, removed.AdjacencyEnumerationCount);
            Assert.IsTrue(GearNetworkDatastore.TryGetGearNetwork(a.BlockInstanceId, out var networkA));
            Assert.IsTrue(GearNetworkDatastore.TryGetGearNetwork(c.BlockInstanceId, out var networkC));
            Assert.AreSame(networkA, networkC);
        }

        [Test]
        public void dirtyでない再構築は適用済みmapを維持する()
        {
            var datastore = new GearNetworkDatastore();
            GearNetworkDatastore.AddGear(new FakeGear(1));
            datastore.RebuildIfDirty();
            var appliedMap = GearNetworkDatastoreReflectionTestUtil.GetTopologyMap(datastore);

            datastore.RebuildIfDirty();

            Assert.AreSame(appliedMap, GearNetworkDatastoreReflectionTestUtil.GetTopologyMap(datastore));
        }

        private sealed class FakeGear : IGearEnergyTransformer
        {
            private readonly List<GearConnect> _connections = new();

            public FakeGear(int id)
            {
                BlockInstanceId = new BlockInstanceId(id);
            }

            public BlockInstanceId BlockInstanceId { get; }
            public RPM CurrentRpm => new(0);
            public Torque CurrentTorque => new(0);
            public bool IsCurrentClockwise => true;
            public bool IsDestroy { get; private set; }
            public int AdjacencyEnumerationCount { get; private set; }

            public static void ConnectEachOther(FakeGear a, FakeGear b)
            {
                a._connections.Add(new GearConnect(b, default, default));
                b._connections.Add(new GearConnect(a, default, default));
            }

            public Torque GetRequiredTorque(RPM rpm, bool isClockwise)
            {
                return new Torque(0);
            }

            public void NotifyStateChanged()
            {
            }

            public List<GearConnect> GetGearConnects()
            {
                AdjacencyEnumerationCount++;
                return _connections;
            }

            public void Destroy()
            {
                IsDestroy = true;
            }
        }
    }
}
