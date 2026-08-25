using System;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Map.MapObject.Pending;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;

namespace Client.Tests.Map.Pending
{
    /// <summary>
    ///     生成前に届いた破壊/HPがスナップショットを上書きして適用されることを検証
    ///     Verifies that destroy/HP arriving before instantiation is applied over the snapshot
    /// </summary>
    public class MapObjectPendingStateApplierTest
    {
        // ForUnitTestに実在するmapObjectのguid。MasterHolder経由のInitializeを実際に通すため既存テストと同じ値を使う
        // A mapObject guid that actually exists in ForUnitTest, reused from the existing tests so Initialize resolves through MasterHolder for real
        private static readonly Guid ExistingMapObjectGuid = new("00000000-0000-2222-0000-000000000001");

        private const int SnapshotHp = 100;

        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void 保留HPが個体へ適用される()
        {
            var mapObject = CreateSnapshotAppliedMapObject(false);

            MapObjectPendingStateApplier.Apply(mapObject, new MapObjectPendingState(false, true, 30));

            Assert.AreEqual(30, mapObject.CurrentHp);
            Assert.IsFalse(mapObject.IsDestroyed);
        }

        [Test]
        public void 保留破壊が個体へ適用される()
        {
            var mapObject = CreateSnapshotAppliedMapObject(false);

            MapObjectPendingStateApplier.Apply(mapObject, new MapObjectPendingState(true, false, 0));

            Assert.IsTrue(mapObject.IsDestroyed);
        }

        [Test]
        public void 保留状態はスナップショットより優先される()
        {
            // 生存状態へ保留破壊とHPを被せる
            // Override a live snapshot with pending destroy and HP
            var mapObject = CreateSnapshotAppliedMapObject(false);
            Assert.AreEqual(SnapshotHp, mapObject.CurrentHp);

            MapObjectPendingStateApplier.Apply(mapObject, new MapObjectPendingState(true, true, 5));

            Assert.IsTrue(mapObject.IsDestroyed);
            Assert.AreEqual(5, mapObject.CurrentHp);
        }

        [Test]
        public void 破壊済みスナップショットへの保留破壊は再発火しない()
        {
            var mapObject = CreateSnapshotAppliedMapObject(true);
            var destroyedEventCount = 0;
            mapObject.OnDestroyMapObject.Subscribe(_ => destroyedEventCount++);

            MapObjectPendingStateApplier.Apply(mapObject, new MapObjectPendingState(true, false, 0));

            Assert.AreEqual(0, destroyedEventCount);
        }

        private MapObjectGameObject CreateSnapshotAppliedMapObject(bool isDestroyedInSnapshot)
        {
            _root = new GameObject("MapObjectPendingStateApplierTestRoot");
            var mapObject = _root.AddComponent<MapObjectGameObject>();

            mapObject.SetRuntimeIdentity(1, ExistingMapObjectGuid.ToString());
            mapObject.Initialize(new GetMapObjectInfoProtocol.MapObjectsInfoMessagePack(1, isDestroyedInSnapshot, SnapshotHp));
            return mapObject;
        }
    }
}
