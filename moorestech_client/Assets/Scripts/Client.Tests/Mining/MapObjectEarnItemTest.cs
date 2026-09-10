using System;
using Client.Game.InGame.Map.MapObject;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Mining
{
    /// <summary>
    ///     mapObjectのマスタ→取得物Guid解決を固定する（露頭側はOutcropMiningTargetTestが持つ）
    ///     Pins the mapObject master-to-earned-guid resolution; the outcrop side lives in OutcropMiningTargetTest
    /// </summary>
    public class MapObjectEarnItemTest
    {
        private static readonly Guid MiningRockGuid = new("00000000-0000-2222-0000-000000000001");
        private static readonly Guid DecorationGuid = new("00000000-0000-4444-0000-000000000001");
        private static readonly Guid MiningRockEarnItemGuid = new("00000000-0000-0000-1234-000000000002");

        private GameObject _parentObject;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _parentObject = new GameObject("MapObjects");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_parentObject);
        }

        [Test]
        public void 採掘可能なmapObjectはマスタのearnItemsを取得物として解決する()
        {
            var mapObject = CreateMapObject(MiningRockGuid);

            CollectionAssert.AreEqual(new[] { MiningRockEarnItemGuid }, mapObject.EarnItemGuids);
        }

        [Test]
        public void 装飾物のmapObjectは取得物を持たない()
        {
            // miningType None かつ miningParam 空。IMinableMapObjectParamでない個体が空を返すことを固定する
            // miningType None with an empty miningParam; pins the empty result for a param that is not IMinableMapObjectParam
            var mapObject = CreateMapObject(DecorationGuid);

            CollectionAssert.IsEmpty(mapObject.EarnItemGuids);
        }

        private MapObjectGameObject CreateMapObject(Guid mapObjectGuid)
        {
            var mapObjectObject = new GameObject("MapObject");
            mapObjectObject.transform.SetParent(_parentObject.transform);
            var mapObject = mapObjectObject.AddComponent<MapObjectGameObject>();
            mapObject.SetRuntimeIdentity(1, mapObjectGuid.ToString());
            mapObject.Initialize(new GetMapObjectInfoProtocol.MapObjectsInfoMessagePack(1, false, 30));
            return mapObject;
        }
    }
}
