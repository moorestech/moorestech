using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util.AnchorRelative;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UnitTest.Tutorial
{
    public class AnchorRelativeDirectionUtilTest
    {
        private static readonly BlockDirection[] HorizontalAnchors = { BlockDirection.North, BlockDirection.East, BlockDirection.South, BlockDirection.West };

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

        // 期待値の根拠: クォータニオン合成と前方・上方の両軸で一致すること
        // Ground truth: composition must match quaternion multiplication on both the forward and up axis
        [Test]
        public void RotateByAnchorMatchesQuaternionComposition()
        {
            foreach (var anchor in HorizontalAnchors)
            foreach (BlockDirection local in Enum.GetValues(typeof(BlockDirection)))
            {
                var expectedRotation = anchor.GetRotation() * local.GetRotation();
                var actual = AnchorRelativeDirectionUtil.RotateByAnchor(local, anchor);
                var actualRotation = actual.GetRotation();

                Assert.AreEqual(Vector3Int.RoundToInt(expectedRotation * Vector3.forward), Vector3Int.RoundToInt(actualRotation * Vector3.forward), $"forward local={local} anchor={anchor} actual={actual}");
                Assert.AreEqual(Vector3Int.RoundToInt(expectedRotation * Vector3.up), Vector3Int.RoundToInt(actualRotation * Vector3.up), $"up local={local} anchor={anchor} actual={actual}");
            }
        }
    }
}
