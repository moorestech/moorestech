using System;
using Client.Game.InGame.Tutorial;
using Mooresmaster.Model.ChallengesModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.UnitTest.Tutorial
{
    public class MapObjectPinTargetResolverTest
    {
        // forUnitTest map.json: TreeTest/TestMiningRock/TestRubbleRock が item ...0002 を落とし、vanilla:Tree だけが ...0001 を落とす
        // forUnitTest map.json: TreeTest/TestMiningRock/TestRubbleRock drop item ...0002, only vanilla:Tree drops ...0001
        private static readonly Guid TreeTestGuid = Guid.Parse("00000000-0000-1111-0000-000000000001");
        private static readonly Guid MiningRockGuid = Guid.Parse("00000000-0000-2222-0000-000000000001");
        private static readonly Guid RubbleRockGuid = Guid.Parse("00000000-0000-3333-0000-000000000001");
        private static readonly Guid Item2Guid = Guid.Parse("00000000-0000-0000-1234-000000000002");

        // Test3はitems.jsonに実在するがどのmapObjectのearnItemsにも無い
        // Test3 exists in items.json but no mapObject earns it
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

            var result = MapObjectPinTargetResolver.ResolveMapObjectGuids(param);

            CollectionAssert.AreEqual(new[] { TreeTestGuid }, result);
        }

        [Test]
        public void EarnItem指定はそのアイテムを落とす全MapObjectへ解決される()
        {
            var param = new MapObjectPinTutorialParam(
                MapObjectPinTutorialParam.PinTargetTypeConst.earnItem,
                new EarnItemPinTargetParam(Item2Guid),
                "pin");

            var result = MapObjectPinTargetResolver.ResolveMapObjectGuids(param);

            CollectionAssert.AreEquivalent(new[] { TreeTestGuid, MiningRockGuid, RubbleRockGuid }, result);
        }

        [Test]
        public void EarnItem指定が誰も落とさないアイテムなら空へ解決される()
        {
            var param = new MapObjectPinTutorialParam(
                MapObjectPinTutorialParam.PinTargetTypeConst.earnItem,
                new EarnItemPinTargetParam(NobodyEarnsItemGuid),
                "pin");

            var result = MapObjectPinTargetResolver.ResolveMapObjectGuids(param);

            CollectionAssert.IsEmpty(result);
        }
    }
}
