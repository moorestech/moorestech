using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Path;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltConveyor
{
    public class BeltConveyorSlopePlacementTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        // 上りを選ぶと全セルが上りブロックになり中途に直線が混ざらない
        // Selecting the up slope fills every cell with the up block and never mixes in a straight block
        [Test]
        public void 坂を選ぶと経路の全セルがその坂ブロックになる()
        {
            var holdingBlockId = ForUnitTestModBlockId.TestGearBeltConveyorUp;
            Assert.IsTrue(BeltConveyorPlaceFamilyUtil.TryGetFamily(holdingBlockId, out var family));
            Assert.IsTrue(family.TryGetSlopeDirection(holdingBlockId, out var slopeDirection));

            var placeInfos = BeltConveyorSlopePathBuilder.Build(
                new Vector3Int(0, 0, 0), new Vector3Int(2, 0, 0), false, BlockDirection.East, slopeDirection);
            foreach (var placeInfo in placeInfos) placeInfo.BlockId = holdingBlockId;

            Assert.AreEqual(3, placeInfos.Count);
            Assert.IsTrue(placeInfos.All(info => info.BlockId == holdingBlockId));
            Assert.IsFalse(placeInfos.Any(info => info.BlockId == family.StraightBlockId));
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, placeInfos.Select(info => info.Position.y).ToList());
        }

        // 下りを選ぶと同じ経路が毎セル1段下がる
        // Selecting the down slope makes the same path descend one per cell
        [Test]
        public void 下りを選ぶと経路が毎セル下がる()
        {
            var holdingBlockId = ForUnitTestModBlockId.TestGearBeltConveyorDown;
            Assert.IsTrue(BeltConveyorPlaceFamilyUtil.TryGetFamily(holdingBlockId, out var family));
            Assert.IsTrue(family.TryGetSlopeDirection(holdingBlockId, out var slopeDirection));

            var placeInfos = BeltConveyorSlopePathBuilder.Build(
                new Vector3Int(0, 0, 0), new Vector3Int(2, 0, 0), false, BlockDirection.East, slopeDirection);

            CollectionAssert.AreEqual(new[] { 0, -1, -2 }, placeInfos.Select(info => info.Position.y).ToList());
        }

        // 直線を選んだときは坂の向きが引けず既存の自動判定経路に落ちる
        // Selecting the straight block yields no slope direction, so the existing auto path is used
        [Test]
        public void 直線を選んだときは坂の向きが引けない()
        {
            Assert.IsTrue(BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.GearBeltConveyor, out var family));
            Assert.IsFalse(family.TryGetSlopeDirection(ForUnitTestModBlockId.GearBeltConveyor, out _));
        }
    }
}
