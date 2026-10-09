using Core.Master;
using Game.Block.Interface;
using Game.Blueprint;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    public class BlueprintCopyTargetRuleTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void RailFamilyIsExcludedTest()
        {
            var rail = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestTrainRail);
            var chest = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId);

            // レールのみ除外
            // Exclude rail-family blocks while retaining ordinary blocks
            var box = BlueprintCopyTargetRule.CreateBox(Vector3Int.zero, Vector3Int.zero);
            var position = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one);
            Assert.IsFalse(BlueprintCopyTargetRule.IsCopiedByBox(rail, position, box));
            Assert.IsTrue(BlueprintCopyTargetRule.IsCopiedByBox(chest, position, box));
        }

        [Test]
        public void OneOccupiedCellInsideBoxIncludesMultiCellBlockTest()
        {
            var blockId = ForUnitTestModBlockId.MultiBlockGeneratorId;
            var master = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var position = new BlockPositionInfo(new Vector3Int(10, 0, 10), BlockDirection.North, master.BlockSize);
            Assert.AreNotEqual(position.MinPos, position.MaxPos);

            // 端1セルの重なりも対象
            // Include the block when only its maximum occupied cell intersects
            var insideBox = BlueprintCopyTargetRule.CreateBox(position.MaxPos, position.MaxPos);
            Assert.IsTrue(BlueprintCopyTargetRule.IsCopiedByBox(master, position, insideBox));
            var outside = position.MaxPos + Vector3Int.right;
            Assert.IsFalse(BlueprintCopyTargetRule.IsCopiedByBox(master, position, BlueprintCopyTargetRule.CreateBox(outside, outside)));
        }
    }
}
