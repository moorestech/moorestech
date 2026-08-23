using System;
using System.Collections.Generic;
using Client.Game.InGame.Map.MapObject;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Map
{
    /// <summary>
    ///     - 破壊済み個体の除外を検証
    ///     - guid分離を検証（実体で）
    ///     - Verifies destroyed-object exclusion
    ///     - Verifies guid separation (with real objects)
    /// </summary>
    public class MapObjectNearestSearcherTest
    {
        // ForUnitTestに実在するmapObjectのguid（MapObjectHpBarScaleTestと同じ）
        // A mapObject guid that exists in ForUnitTest (same as MapObjectHpBarScaleTest)
        private static readonly Guid ExistingMapObjectGuid = new("00000000-0000-2222-0000-000000000001");
        // 同じくForUnitTestのmap.jsonに実在する別guid（guid分離の検証用）
        // Another guid that exists in ForUnitTest map.json (for guid-separation checks)
        private static readonly Guid OtherMapObjectGuid = new("00000000-0000-1111-0000-000000000001");

        // 探索APIは候補集合を受けるので、単一guid検証でも集合に包んで渡す
        // The search API takes a candidate set, so single-guid checks wrap the guid in a set
        private static readonly HashSet<Guid> ExistingTargets = new() { ExistingMapObjectGuid };

        private readonly List<GameObject> _created = new();

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created) UnityEngine.Object.DestroyImmediate(created);
            _created.Clear();
        }

        [Test]
        public void 破壊済み個体は次の探索から返らない()
        {
            var searcher = new MapObjectNearestSearcher();
            var near = CreateMapObject(1, ExistingMapObjectGuid, new Vector3(1f, 0f, 0f), false);
            var far = CreateMapObject(2, ExistingMapObjectGuid, new Vector3(10f, 0f, 0f), false);
            searcher.Register(near);
            searcher.Register(far);
            Assert.AreSame(near, searcher.SearchNearest(ExistingTargets, Vector3.zero));

            near.DestroyMapObject();
            searcher.MarkDirty(ExistingMapObjectGuid);
            Assert.AreSame(far, searcher.SearchNearest(ExistingTargets, Vector3.zero));

            far.DestroyMapObject();
            searcher.MarkDirty(ExistingMapObjectGuid);
            Assert.IsNull(searcher.SearchNearest(ExistingTargets, Vector3.zero));
        }

        [Test]
        public void 初期スナップショットで破壊済みの個体は最初から返らない()
        {
            var searcher = new MapObjectNearestSearcher();
            var destroyedAtStart = CreateMapObject(1, ExistingMapObjectGuid, new Vector3(1f, 0f, 0f), true);
            var alive = CreateMapObject(2, ExistingMapObjectGuid, new Vector3(10f, 0f, 0f), false);
            searcher.Register(destroyedAtStart);
            searcher.Register(alive);
            Assert.AreSame(alive, searcher.SearchNearest(ExistingTargets, Vector3.zero));
        }

        [Test]
        public void 別guidの近い個体は返さない()
        {
            var searcher = new MapObjectNearestSearcher();
            var nearOther = CreateMapObject(1, OtherMapObjectGuid, new Vector3(1f, 0f, 0f), false);
            var farTarget = CreateMapObject(2, ExistingMapObjectGuid, new Vector3(10f, 0f, 0f), false);
            searcher.Register(nearOther);
            searcher.Register(farTarget);
            Assert.AreSame(farTarget, searcher.SearchNearest(ExistingTargets, Vector3.zero));
        }

        [Test]
        public void 未登録guidはnullを返す()
        {
            var searcher = new MapObjectNearestSearcher();
            Assert.IsNull(searcher.SearchNearest(ExistingTargets, Vector3.zero));
        }

        [Test]
        public void 候補集合に複数guidがあるとその中の最寄りが返る()
        {
            var searcher = new MapObjectNearestSearcher();
            var nearOther = CreateMapObject(1, OtherMapObjectGuid, new Vector3(1f, 0f, 0f), false);
            var farTarget = CreateMapObject(2, ExistingMapObjectGuid, new Vector3(10f, 0f, 0f), false);
            searcher.Register(nearOther);
            searcher.Register(farTarget);

            var candidates = new HashSet<Guid> { ExistingMapObjectGuid, OtherMapObjectGuid };
            Assert.AreSame(nearOther, searcher.SearchNearest(candidates, Vector3.zero));
            Assert.AreSame(farTarget, searcher.SearchNearest(candidates, new Vector3(11f, 0f, 0f)));
        }

        private MapObjectGameObject CreateMapObject(int instanceId, Guid guid, Vector3 position, bool isDestroyed)
        {
            var gameObject = new GameObject($"MapObject_{instanceId}");
            gameObject.transform.position = position;
            _created.Add(gameObject);
            var mapObject = gameObject.AddComponent<MapObjectGameObject>();
            mapObject.SetRuntimeIdentity(instanceId, guid.ToString());
            mapObject.Initialize(new GetMapObjectInfoProtocol.MapObjectsInfoMessagePack(instanceId, isDestroyed, 1));
            return mapObject;
        }
    }
}
