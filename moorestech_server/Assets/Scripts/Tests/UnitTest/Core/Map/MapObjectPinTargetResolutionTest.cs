using System;
using Core.Master;
using Mooresmaster.Model.ChallengesModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Map
{
    public class MapObjectPinTargetResolutionTest
    {
        // fixtureのmapObject。いずれもitem...0002を落とす
        // Fixture map objects; all three earn item ...0002
        private static readonly Guid TreeTestGuid = Guid.Parse("00000000-0000-1111-0000-000000000001");
        private static readonly Guid MiningRockGuid = Guid.Parse("00000000-0000-2222-0000-000000000001");
        private static readonly Guid RubbleRockGuid = Guid.Parse("00000000-0000-3333-0000-000000000001");

        private static readonly Guid Item2Guid = Guid.Parse("00000000-0000-0000-1234-000000000002");

        // Test3は実在するが誰も落とさない
        // Test3 exists but nothing earns it
        private static readonly Guid NobodyEarnsItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000003");

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void MapObject指定は単一のGuidへ解決される()
        {
            var param = new MapObjectPinTutorialParam(
                MapObjectPinTutorialParam.PinTargetTypeConst.mapObject,
                new MapObjectPinTargetParam(TreeTestGuid),
                "pin");

            var result = MasterHolder.MapObjectMaster.ResolvePinTargets(param);

            CollectionAssert.AreEquivalent(new[] { TreeTestGuid }, result);
        }

        [Test]
        public void EarnItem指定はそのアイテムを落とす全MapObjectへ解決される()
        {
            var param = new MapObjectPinTutorialParam(
                MapObjectPinTutorialParam.PinTargetTypeConst.earnItem,
                new EarnItemPinTargetParam(Item2Guid),
                "pin");

            var result = MasterHolder.MapObjectMaster.ResolvePinTargets(param);

            CollectionAssert.AreEquivalent(new[] { TreeTestGuid, MiningRockGuid, RubbleRockGuid }, result);
        }

        [Test]
        public void EarnItem指定が誰も落とさないアイテムなら空へ解決される()
        {
            var param = new MapObjectPinTutorialParam(
                MapObjectPinTutorialParam.PinTargetTypeConst.earnItem,
                new EarnItemPinTargetParam(NobodyEarnsItemGuid),
                "pin");

            var result = MasterHolder.MapObjectMaster.ResolvePinTargets(param);

            CollectionAssert.IsEmpty(result);
        }

        // 未知の狙い先はマスタ検証が報告するため、例外ではなくfalseで返る必要がある
        // An unknown target must come back as false, not an exception, so master validation can report it
        [Test]
        public void 未知の狙い先指定は解決に失敗する()
        {
            var param = new MapObjectPinTutorialParam(
                MapObjectPinTutorialParam.PinTargetTypeConst.mapObject,
                null,
                "pin");

            var resolved = MasterHolder.MapObjectMaster.TryResolvePinTargets(param, out var result);

            Assert.IsFalse(resolved);
            CollectionAssert.IsEmpty(result);
        }
    }
}
