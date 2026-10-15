using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Game.Block.Interface;
using Mooresmaster.Localization.Generated;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Ground.Entry
{
    public class FootprintGroundedRayEntryTest : FootprintGroundedEntryFixture
    {
        [TestCase(0, 22)]
        [TestCase(1, 23)]
        [TestCase(-2, 20)]
        public void RayEntryUsesFootprintMaximumAndAppliesOffsetOnce(int offset, int expectedY)
        {
            var master = CreateWidePole();
            Assert.IsTrue(PlaceSystemUtil.TryGetRayHitBlockPosition(PlacementCamera, offset, BlockDirection.North, master, out var aimCell, out _));
            Assert.AreEqual(20 + offset, aimCell.y, "照準点のfloorが最高点と異なる前提");

            // 内部セル解決を迂回せず公開レイ入口を検証する
            // Verify the public ray entry rather than invoking internal cell resolution
            var result = PlaceSystemUtil.ResolveFootprintGroundedRayHitBlockPosition(PlacementCamera, offset, BlockDirection.North, master, out var cell, out var surface);

            Assert.AreEqual(FootprintGroundedCellResult.Resolved, result);
            Assert.AreEqual(new Vector3Int(500, expectedY, 599), cell);
            Assert.IsNull(surface);
        }

        [Test]
        public void RayEntryPreservesBlockFaceCellAndOffset()
        {
            var face = CreateFace();
            var result = PlaceSystemUtil.ResolveFootprintGroundedRayHitBlockPosition(PlacementCamera, 2, BlockDirection.North, CreateWidePole(), out var cell, out var surface);

            Assert.AreEqual(FootprintGroundedCellResult.Resolved, result);
            Assert.AreSame(face, surface);
            Assert.AreEqual(new Vector3Int(500, 27, 599), cell);
        }

        [Test]
        public void BoolEntryReportsMissingGround()
        {
            RemoveFootprintGround();
            var feedback = new PlacementFeedback();

            var resolved = PlaceSystemUtil.TryGetFootprintGroundedRayHitBlockPosition(PlacementCamera, 0, BlockDirection.North, CreateWidePole(), feedback, out _, out _);

            Assert.IsFalse(resolved);
            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGroundNotFound.Key, feedback.Lines[0].Key.Key);
        }

        [Test]
        public void BoolEntryKeepsNoAimSurfaceSilent()
        {
            Ground.SetActive(false);
            var feedback = new PlacementFeedback();

            var resolved = PlaceSystemUtil.TryGetFootprintGroundedRayHitBlockPosition(PlacementCamera, 0, BlockDirection.North, CreateWidePole(), feedback, out _, out _);

            Assert.IsFalse(resolved);
            Assert.IsEmpty(feedback.Lines);
        }

        private static BlockMasterElement CreateWidePole()
        {
            return new BlockMasterElement(0, Guid.Empty, "WidePole", "TestBlockType", null, 1, null, "テスト", "テスト", 0, false,
                new Vector3Int(3, 4, 3), null, false, null);
        }
    }
}
