using System;
using System.Collections.Generic;
using Client.Game.InGame.Map.MapObject;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Map.Registry
{
    public class MapObjectRegistryTest
    {
        private static readonly Guid ExistingMapObjectGuid = new("00000000-0000-2222-0000-000000000001");
        private const int SnapshotHp = 100;
        private readonly List<GameObject> _roots = new();

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var root in _roots) UnityEngine.Object.DestroyImmediate(root);
            _roots.Clear();
        }

        [Test]
        public void 保留破壊は登録時に個体へ適用される()
        {
            var registry = new MapObjectRegistry();
            registry.ApplyDestroy(1);
            var mapObject = CreateMapObject(1);

            Assert.IsTrue(registry.TryRegister(mapObject));
            Assert.IsTrue(mapObject.IsDestroyed);
        }

        [Test]
        public void 保留破壊は最寄り索引登録より先に適用される()
        {
            var registry = new MapObjectRegistry();
            registry.ApplyDestroy(1);
            registry.TryRegister(CreateMapObject(1));

            var actual = registry.SearchNearest(new HashSet<Guid> { ExistingMapObjectGuid }, Vector3.zero);

            Assert.IsNull(actual);
        }

        [Test]
        public void 保留HPは登録時に個体へ適用される()
        {
            var registry = new MapObjectRegistry();
            registry.ApplyHp(1, 25);
            var mapObject = CreateMapObject(1);

            registry.TryRegister(mapObject);

            Assert.AreEqual(25, mapObject.CurrentHp);
        }

        [Test]
        public void 重複instanceIdの登録は失敗する()
        {
            var registry = new MapObjectRegistry();

            Assert.IsTrue(registry.TryRegister(CreateMapObject(1)));
            Assert.IsFalse(registry.TryRegister(CreateMapObject(1)));
        }

        [Test]
        public void 消費済み保留状態は別個体へ波及しない()
        {
            var registry = new MapObjectRegistry();
            registry.ApplyHp(1, 25);
            var first = CreateMapObject(1);
            var second = CreateMapObject(2);

            registry.TryRegister(first);
            registry.TryRegister(second);

            Assert.AreEqual(25, first.CurrentHp);
            Assert.AreEqual(SnapshotHp, second.CurrentHp);
        }

        private MapObjectGameObject CreateMapObject(int instanceId)
        {
            var root = new GameObject($"MapObjectRegistryTest-{instanceId}");
            _roots.Add(root);
            var mapObject = root.AddComponent<MapObjectGameObject>();
            mapObject.SetRuntimeIdentity(instanceId, ExistingMapObjectGuid.ToString());
            mapObject.Initialize(new GetMapObjectInfoProtocol.MapObjectsInfoMessagePack(instanceId, false, SnapshotHp));
            return mapObject;
        }
    }
}
