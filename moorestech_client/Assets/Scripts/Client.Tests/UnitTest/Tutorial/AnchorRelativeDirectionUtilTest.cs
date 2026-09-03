using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UnitTest.Tutorial
{
    public class AnchorRelativeDirectionUtilTest
    {
        // アンカーが回った分だけ相対向きも回る
        // The relative direction rotates together with the anchor
        [TestCase(BlockDirection.North, BlockDirection.North, BlockDirection.North)]
        [TestCase(BlockDirection.East, BlockDirection.North, BlockDirection.East)]
        [TestCase(BlockDirection.North, BlockDirection.East, BlockDirection.East)]
        [TestCase(BlockDirection.East, BlockDirection.East, BlockDirection.South)]
        [TestCase(BlockDirection.West, BlockDirection.South, BlockDirection.East)]
        [TestCase(BlockDirection.South, BlockDirection.West, BlockDirection.East)]
        public void RotateByAnchorComposesHorizontalRotation(BlockDirection local, BlockDirection anchor, BlockDirection expected)
        {
            Assert.AreEqual(expected, AnchorRelativeDirectionUtil.RotateByAnchor(local, anchor));
        }
        
        // 期待値の根拠: クォータニオン合成と一致すること
        // Ground truth: composition must match quaternion multiplication of GetRotation
        [Test]
        public void RotateByAnchorMatchesQuaternionComposition()
        {
            BlockDirection[] horizontals = { BlockDirection.North, BlockDirection.East, BlockDirection.South, BlockDirection.West };
            foreach (var anchor in horizontals)
            foreach (var local in horizontals)
            {
                var expectedForward = Vector3Int.RoundToInt(anchor.GetRotation() * (local.GetRotation() * Vector3.forward));
                var actual = AnchorRelativeDirectionUtil.RotateByAnchor(local, anchor);
                var actualForward = actual.GetCoordinateConvertAction()(Vector3Int.forward);
                Assert.AreEqual(expectedForward, actualForward, $"local={local} anchor={anchor} actual={actual}");
            }
        }
    }
}
