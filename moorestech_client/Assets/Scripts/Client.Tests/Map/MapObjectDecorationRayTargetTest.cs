using System;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Mining;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Map
{
    /// <summary>
    ///     装飾物(None)はレイ対象外で採掘不可を検証
    ///     Verifies a None decoration drops out of the ray target and cannot mine
    /// </summary>
    public class MapObjectDecorationRayTargetTest
    {
        private static readonly Guid DecorationGuid = new("00000000-0000-4444-0000-000000000001");
        private static readonly Guid MiningRockGuid = new("00000000-0000-2222-0000-000000000001");

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
        public void 装飾物はレイターゲットのコライダーが無効化され採掘開始もUnavailableになる()
        {
            var (mapObject, rayCollider) = Build(DecorationGuid, false);

            Assert.IsFalse(rayCollider.enabled);
            Assert.IsFalse(mapObject.IsAvailable);
            Assert.AreEqual(MiningStartOutcome.Unavailable, mapObject.TryBeginHandMining(ItemMaster.EmptyItemId, out _, out _));
        }

        [Test]
        public void 採掘可能なmapObjectはレイターゲットのコライダーが有効のまま()
        {
            var (mapObject, rayCollider) = Build(MiningRockGuid, false);

            Assert.IsTrue(rayCollider.enabled);
            Assert.IsTrue(mapObject.IsAvailable);
        }

        [Test]
        public void 破壊済みの採掘可能mapObjectはレイターゲットのコライダーが無効のまま()
        {
            var (mapObject, rayCollider) = Build(MiningRockGuid, true);

            Assert.IsFalse(rayCollider.enabled);
            Assert.IsFalse(mapObject.IsAvailable);
        }

        private (MapObjectGameObject mapObject, Collider rayCollider) Build(Guid mapObjectGuid, bool isDestroyed)
        {
            // prefabと同じ最小構成
            // Minimal shape matching generated prefabs
            _root = new GameObject("MapObjectDecorationRayTargetTestRoot");
            var rayTargetObject = new GameObject("RayTarget");
            rayTargetObject.transform.SetParent(_root.transform, false);
            var rayCollider = rayTargetObject.AddComponent<BoxCollider>();
            rayTargetObject.AddComponent<MapObjectRayTarget>();

            var mapObject = _root.AddComponent<MapObjectGameObject>();
            mapObject.SetRuntimeIdentity(1, mapObjectGuid.ToString());
            mapObject.Initialize(new GetMapObjectInfoProtocol.MapObjectsInfoMessagePack(1, isDestroyed, 10));
            return (mapObject, rayCollider);
        }
    }
}
